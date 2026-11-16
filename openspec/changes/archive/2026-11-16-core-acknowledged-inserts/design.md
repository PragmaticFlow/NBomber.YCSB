# Design

## Context

See proposal.md - Why. `DataGenerator` (one instance shared by all scenario copies) keeps one counter, `_recordCount`:
- `GenerateRandoms` loops keys `1.._recordCount` for the initial load (in `WithInit`);
- `SetRecordCount(settings.RecordCount)` resets it after the load;
- `GetKeyNext()` does `Interlocked.Increment(ref _recordCount)` and returns the key string;
- `GetKeyZipf(IScenarioContext)` draws `context.Random.Zipf((int)_recordCount, 1.3)`;
- `GetKeyLatest(IScenarioContext, 0.99)` draws `Zipf(min(max, int.MaxValue), 0.99)` and returns `max - (rank - 1)` with `max = Interlocked.Read(ref _recordCount)`.

So the reserved insert key is immediately visible to every key chooser. In `YcsbScenario`, the insert case calls `GetKeyNext()` and `CreateValues()` before `Step.Run("insert", ...)` (T2), and the step only calls `ycsbClient.Insert`. `Zipf` is NBomber's extension method on `System.Random` (`NBomber.CSharp.RandomExtensions.Zipf(Random, int, double)`), so the choosers only need a `Random`, not the whole context. Requirements are in `specs/core-workload/spec.md`.

## Goals / Non-Goals

**Goals:**
- Reads, updates, scans and read-modify-writes never target a key whose insert is still in flight.
- YCSB semantics: reserve before, acknowledge after (also on failure), contiguous limit.
- Minimal change inside `DataGenerator` and the insert case; no new classes or packages.
- Key choosers testable with a seeded `Random`.

**Non-Goals:**
- Zipfian constant/scrambling and YCSB's "draw over recordcount + expected new keys, retry above limit" (T6), validation (T1), warm-up (T5), init checks (T4), chunked load (T7).

## Decisions

### Two counters in `DataGenerator`
Replace the mutable `_recordCount` with:

```csharp
private readonly ulong _recordCount = settings.RecordCount;   // initial load size, used by GenerateRandoms
private readonly object _ackLock = new();
private readonly HashSet<ulong> _pendingAcks = [];

private ulong _lastInsertKey = settings.RecordCount;          // last reserved insert key
private ulong _lastAcknowledgedKey = settings.RecordCount;    // highest contiguous acknowledged key
```

Both counters start at `recordcount` (Java: keys `0..recordcount-1` are loaded and `new AcknowledgedCounterGenerator(recordcount)` sets `limit = recordcount - 1`; with our 1-based keys the last loaded key is `recordcount`), so a `DataGenerator` built directly (as in tests) behaves like one after the initial load. `GenerateRandoms` loops `1.._recordCount` (the readonly initial size).

Remove `SetRecordCount` and its call in `YcsbScenario.WithInit`: it only resets the counters to `settings.RecordCount`, which the field initialisers already do and `GenerateRandoms` never changes, so it is a no-op, and a reset would let the counters disagree with what was loaded.

### Reserve and acknowledge

```csharp
public (ulong KeyNum, string Key) GetKeyNext()
{
    var keyNum = Interlocked.Increment(ref _lastInsertKey);
    return (keyNum, BuildKeyName(keyNum, _zeroPadding));
}

public void AcknowledgeInsert(ulong keyNum)
{
    lock (_ackLock)
    {
        _pendingAcks.Add(keyNum);

        var limit = _lastAcknowledgedKey;
        while (_pendingAcks.Remove(limit + 1))
            limit++;

        Interlocked.Exchange(ref _lastAcknowledgedKey, limit);
    }
}

public ulong LastAcknowledgedKey => Interlocked.Read(ref _lastAcknowledgedKey);
```

- `GetKeyNext` returns the key number too, because the insert must acknowledge it. A tuple keeps one call site and no new type.
- The lock is taken only by acknowledgements (inserts are 5% of D and E). Given every reservation is acknowledged (see the insert case), `_pendingAcks` holds at most one key per in-flight insert, so at most `threadcount` entries; no window size limit is needed (Java uses a fixed 2^20-slot ring because it is lock-free on the fast path).
- `LastAcknowledgedKey` mirrors Java's `lastValue()` and is what the key choosers and tests read. It is a public read-only property; nothing else changes the counters.
- **Alternative: a port of Java's `AcknowledgedCounterGenerator` ring buffer as its own class**: rejected; more code and a new class for the same behaviour at our insert rates.
- **Alternative: acknowledge before the step**: rejected; it reopens the bug.

### Key choosers take `Random`
Change `GetKeyZipf(IScenarioContext context)` to `GetKeyZipf(Random random)` and `GetKeyLatest(IScenarioContext context, double theta = 0.99)` to `GetKeyLatest(Random random, double theta = 0.99)`, using `random.Zipf(...)` and `LastAcknowledgedKey` instead of `_recordCount`. The bodies are otherwise unchanged (same `(int)` cast for zipfian and the same clamp and offset logic for latest), so the distributions are unchanged. `YcsbScenario` passes `context.Random`, as it already does for `NextScanLength`. The `using NBomber.Contracts;` import is removed if no longer used.

### Insert case in `YcsbScenario`

```csharp
case OperationType.Insert:
{
    var values = dataGen.CreateValues();
    var (keyNum, key) = dataGen.GetKeyNext();

    try
    {
        await Step.Run("insert", context, () => ycsbClient.Insert(table: tableName, key, values));
    }
    finally
    {
        dataGen.AcknowledgeInsert(keyNum);
    }
    break;
}
```

A reserved key must always reach `AcknowledgeInsert`; otherwise the limit freezes for the rest of the run and `_pendingAcks` grows. So values are built first, the key is reserved immediately before the `try`, and nothing can throw in between. The `finally` is required, not defensive: NBomber marks an iteration with a failed step as failed and restarts it (NBomber.xml), which can leave `Step.Run` by throwing; the exception must not be caught or swallowed. `finally` acknowledges on success, on a `Fail` response and on an exception, as YCSB does. The acknowledgement is outside the measured step. The other five cases only change `context` to `context.Random` in their key chooser call. Step names, call order and `context.Random` consumption order are unchanged.

### Effect on workloads
- D: read latest returns the newest acknowledged key, so no fast misses from in-flight inserts.
- E: scans start only at acknowledged keys (negligible change).
- A, B, C, F: no inserts, so `LastAcknowledgedKey == recordcount` for the whole run (same as today).
- Warm-up inserts acknowledge the same way.

### Hot-path cost
Reads: one `Interlocked.Read` instead of a plain or interlocked read. Inserts: one short lock after the measured step. Nothing added inside measured steps.

### Destructive operations
None. Tests are offline; the optional live D run needs explicit approval.

## Risks / Trade-offs

- [A failed insert is still acknowledged, so later reads may target a key that does not exist] → Same as YCSB; it shows up as "no data" like any other miss.
- [A reserved key that is never acknowledged freezes the limit for the whole run] → Values are built before reservation, and `finally` acknowledges even when NBomber leaves `Step.Run` by throwing after a failed step; the exception is not caught.
- [The "acknowledge on failure or throw" path has no offline test] → Checked in code review; testing it would need running NBomber with a fake client.
- [Public `LastAcknowledgedKey` and the tuple return of `GetKeyNext` change `DataGenerator`'s surface] → Only `YcsbScenario` and tests use these members.
- [Live `Workload_D` tests count Ok + Fail together and cannot see the fix] → Offline tests cover the logic; an optional live check of the fail count needs approval.

## Migration Plan

No migration. Command lines and data are unchanged. Rollback is reverting the change.
