# Design

## Context

See proposal.md - Why. Current code in `DataGenerator`:

```csharp
public string GetKeyZipf(Random random)
{
    var keyNum = random.Zipf((int)LastAcknowledgedKey, 1.3);
    return BuildKeyName((ulong)keyNum, _zeroPadding);
}
```

`GetKeyZipf` is called with NBomber's per-copy `context.Random` for read, update, scan and read-modify-write (`YcsbScenario`). `LastAcknowledgedKey` is the highest key with every key up to it loaded or acknowledged (T3); it starts at `recordcount` and only grows. Keys are 1-based (`user` + padding + number, xxHash for hashed insert order). `new DataGenerator(settings)` runs in `YcsbScenario.Run` before NBomber starts and before `InitDb`/`DeleteAllData`. Class-level constants go at the top of the class in UPPER_CASE; public one-line members sit in one block after the fields (project convention).

YCSB reference (Java, verified line by line against the source; paths under `core/src/main/java/site/ycsb/`):
- `CoreWorkload.init`: `recordcount` (0 → `Integer.MAX_VALUE`, L434-437); `insertstart` default 0; `insertcount` default `recordcount - insertstart` (L448-451). Only for `requestdistribution=zipfian`: `opcount` = total `operationcount` (L516), `expectednewkeys = (int)(opcount * insertproportion * 2.0)` (L517), `keychooser = new ScrambledZipfianGenerator(insertstart, insertstart + insertcount + expectednewkeys)` (L519). For `latest` (workload D) a `SkewedLatestGenerator` is used instead (L521) and this item count is not built. `nextKeynum` redraws while `keynum > transactioninsertkeysequence.lastValue()` (L715-717); the acknowledged counter starts at `recordcount - 1` (`AcknowledgedCounterGenerator.java` L43).
- `ScrambledZipfianGenerator(min, max)`: `itemcount = max - min + 1` (L87, inclusive); `gen = new ZipfianGenerator(0, ITEM_COUNT, 0.99, ZETAN)` with `ITEM_COUNT = 10_000_000_000`, `ZETAN = 26.46902820178302` (L33-35, L89); `nextValue() = min + fnvhash64(gen.nextValue()) % itemcount` (L103).
- `ZipfianGenerator(0, ITEM_COUNT, θ, zetan)`: `items = max - min + 1 = ITEM_COUNT + 1` (L134); `zeta2theta = zeta(2, θ)` (L140); `alpha = 1/(1-θ)` (L142); `countforzeta = items` (L144); `eta = (1 - (2/items)^(1-θ)) / (1 - zeta2theta/zetan)` (L145). `nextValue()` calls `nextLong(items)` (L274), so `itemcount == countforzeta` and zeta is never recomputed. `nextLong`: `u = uniform[0,1)`; `uz = u*zetan`; `uz < 1` → 0; `uz < 1 + 0.5^θ` → 1 (Java computes the pow on every draw, L257); else `(long)(items * (eta*u - eta + 1)^alpha)` (L261).
- `Utils.fnvhash64(long)` (L43-65): offset basis `0xCBF29CE484222325`, prime `1099511628211`; 8 octets: `octet = val & 0xff; val >>= 8; hash ^= octet; hash *= prime` (64-bit wrap); returns `Math.abs(hash)`.
- Insert proportion per workload file: A, B, C, F 0 (property absent, default 0.0); D 0.05 with `requestdistribution=latest`; E 0.05 with zipfian.

Requirements are in `specs/core-workload/spec.md`.

## Goals / Non-Goals

**Goals:**
- `GetKeyZipf` produces YCSB's scrambled zipfian over `1 .. itemcount`, redrawing keys above `LastAcknowledgedKey`.
- One-time precomputation; per draw only cheap work.
- No new classes or packages; the generator lives in `DataGenerator` as constants and static helpers.
- Deterministic tests: FNV vectors and rank-to-key mapping with a scripted `Random` (`NextDouble` override); invariant and shape tests with a seeded `Random`.

**Non-Goals:**
- Read latest (`GetKeyLatest`, workload D) - recorded as a separate follow-up (see below).
- `requestdistribution` options, key-name hashing, CLI validation (T1), chunked load.

## Decisions

### Constants (top of `DataGenerator`, UPPER_CASE)

```csharp
private const double ZIPFIAN_CONSTANT = 0.99;
private const double ZETAN = 26.46902820178302;              // zeta(10^10, 0.99), YCSB ScrambledZipfianGenerator.ZETAN
private const long ZIPFIAN_ITEMS = 10_000_000_001L;          // YCSB ITEM_COUNT + 1 (items = max - min + 1)
private const long FNV_OFFSET_BASIS_64 = unchecked((long)0xCBF29CE484222325);
private const long FNV_PRIME_64 = 1099511628211L;

private static readonly double ZIPFIAN_ZETA_2_THETA = 1 + 1 / Math.Pow(2, ZIPFIAN_CONSTANT);   // Java zeta(2, theta)
private static readonly double ZIPFIAN_RANK_1_LIMIT = 1.0 + Math.Pow(0.5, ZIPFIAN_CONSTANT);   // nextLong branch threshold
private static readonly double ZIPFIAN_ALPHA = 1.0 / (1.0 - ZIPFIAN_CONSTANT);
private static readonly double ZIPFIAN_ETA =
    (1 - Math.Pow(2.0 / ZIPFIAN_ITEMS, 1 - ZIPFIAN_CONSTANT)) / (1 - ZIPFIAN_ZETA_2_THETA / ZETAN);
```

The derived values are `static readonly` because `Math.Pow` is not a constant expression; they use the same UPPER_CASE naming and sit right after the constants. `ZIPFIAN_ZETA_2_THETA` uses Java's `zeta(2, θ)` form; `ZIPFIAN_RANK_1_LIMIT` precomputes the `1 + 0.5^θ` threshold that Java recomputes on every draw. `Math.Pow` may differ from Java's by an ulp, so parity of floating-point draws is statistical; the FNV hash and the rank-to-key mapping are exact integer arithmetic.

### Item count and empty-dataset guard

```csharp
private readonly long _zipfianItemCount = GetZipfianItemCount(settings);

private static long GetZipfianItemCount(YcsbCliArgs settings)
{
    var operations = WorkloadManager.GetOperations(settings.Workload);
    var usesZipfian = operations.Any(o => o.Item1 is OperationType.Read or OperationType.Update
                                                  or OperationType.Scan or OperationType.ReadModifyWrite);
    if (usesZipfian && settings.RecordCount == 0)
        throw new ArgumentException(
            $"recordcount must be at least 1 for workload {settings.Workload}: read, update, scan and read-modify-write choose keys from loaded records.",
            nameof(settings));

    var insertProportion = (double)operations.Where(o => o.Item1 == OperationType.Insert).Sum(o => o.Item2)
                           / operations.Sum(o => o.Item2);
    var expectedNewKeys = Math.Max(0, (long)(settings.OperationCount * insertProportion * 2.0));

    // + 1: YCSB builds ScrambledZipfianGenerator(insertstart, insertstart + insertcount + expectednewkeys),
    // an inclusive range (CoreWorkload.java L519, ScrambledZipfianGenerator.java L87).
    try
    {
        return checked((long)settings.RecordCount + expectedNewKeys + 1);
    }
    catch (OverflowException)
    {
        throw new ArgumentException($"recordcount ({settings.RecordCount}) is too large for zipfian key choice.", nameof(settings));
    }
}
```

**Large values (user requirement).** `OperationCount` is an `int`, so `OperationCount * insertProportion * 2.0` is at most about 4.3·10^9, exactly representable in a double (below 2^53) and truncated like Java's `(int)` cast, without loss of precision. `RecordCount` is a `ulong`; the `checked` conversion and addition turn any unrepresentable value into an `ArgumentException` naming recordcount instead of a silent wrap. Example: workload E with `recordcount = operationcount = 2147483647` gives `2147483647 + 214748364 + 1 = 2362232012` (needs `long`, works). The item count is only stored and used as a modulus; the dataset itself is not affected by this change.

- Reuses the workload's operation mix: insert proportion 5/100 for D and E (the same double as YCSB's 0.05), 0 otherwise. Item counts at recordcount 1000 and operationcount 1000: A, B, C, F 1001; E 1101. D also computes 1101 but never calls `GetKeyZipf` (in YCSB, D does not build this generator).
- `Math.Max(0, ...)` keeps the item count positive for a negative `operationcount` until T1 validates it.
- `public long ZipfianItemCount => _zipfianItemCount;` is added to the public one-line block (next to `LastAcknowledgedKey`) for tests and diagnostics.

**Empty dataset (`recordcount 0`).** YCSB treats `recordcount 0` as "unspecified" and replaces it with `Integer.MAX_VALUE` (a huge key space where most reads miss); that has no meaning with our in-memory load. Without a guard, the redraw loop would never end (the limit stays 0 for A, B, C, F; for E with one copy the copy never reaches an insert). So the generator rejects `recordcount 0` when it is created, for workloads that choose zipfian keys (A, B, C, E, F):
- **Why the constructor:** it runs before NBomber starts and before the destructive `InitDb`/`DeleteAllData`, costs nothing per draw, and afterwards the limit is always ≥ 1 and only grows, so the redraw loop always ends. The exception propagates out of the `run` action the same way `GetYcsbClient`'s `NotSupportedException` already does (Program.cs).
- **Not at the first draw:** a throw inside a step would be counted as failed iterations rather than stopping the run.
- **Not via T1 validation:** this is the generator's own invariant (it also covers settings built directly, as in tests); a friendlier CLI rule can be added in T1 later.
- **D unchanged:** D uses read latest, not zipfian; its existing `max <= 1 → key 1` fallback is part of the read-latest follow-up.

### Draw

```csharp
public string GetKeyZipf(Random random)
{
    // limit >= 1: the constructor rejects recordcount 0 for workloads that call this
    var limit = LastAcknowledgedKey;

    long keyNum;
    do
    {
        keyNum = 1 + FnvHash64(NextZipfianRank(random)) % _zipfianItemCount;
    }
    while (keyNum < 1 || (ulong)keyNum > limit);

    return BuildKeyName((ulong)keyNum, _zeroPadding);
}

private static long NextZipfianRank(Random random)
{
    var u = random.NextDouble();
    var uz = u * ZETAN;

    if (uz < 1.0)
        return 0;

    if (uz < ZIPFIAN_RANK_1_LIMIT)
        return 1;

    return (long)(ZIPFIAN_ITEMS * Math.Pow(ZIPFIAN_ETA * u - ZIPFIAN_ETA + 1, ZIPFIAN_ALPHA));
}

private static long FnvHash64(long value)
{
    var hash = FNV_OFFSET_BASIS_64;

    for (var i = 0; i < 8; i++)
    {
        var octet = value & 0xff;
        value >>= 8;
        hash ^= octet;
        hash = unchecked(hash * FNV_PRIME_64);
    }

    return hash == long.MinValue ? hash : Math.Abs(hash);
}
```

- `1 +` maps YCSB's 0-based keys to our 1-based keys: Java loads 0..rc-1 with limit rc-1, we load 1..rc with limit rc, so `k(ours) = k(java) + 1` keeps the redraw condition `k > limit` equivalent.
- `FnvHash64` and `NextZipfianRank` are private (user decision: no production API only for tests). The hash is verified through the observable keys: the exact rank-to-key mapping at two item counts (1001 and 1000001) and the small-dataset redraws pin `fnv(0)`, `fnv(1)` and `fnv(2)` modulo several values.
- `Math.Abs(long.MinValue)` throws in .NET while Java returns `long.MinValue`; the guard keeps Java's value. A negative hash gives a negative remainder, so `keyNum` can be 0 or negative; the key is kept as `long` and the loop redraws anything below 1 (only possible if a rank hashes exactly to `long.MinValue`).
- `(long)` truncation matches Java: `eta*u - eta + 1` lies in (0.788, 1], so the result is in 0..10^10 with no NaN or saturation.
- The limit is read once per call. Redraws: about 0.1% of draws for A, B, C, F (key `itemcount` is above the limit); at the start of E, 101 of 1101 keys are above the limit (about 9% or more of draws).
- `using NBomber.CSharp;` stays (still used by `GetKeyLatest`).
- **Alternative: keep NBomber `Zipf(n, 0.99)` without scrambling**: rejected; hot keys would sit at the lowest key numbers (together under ordered inserts), and the shape differs from YCSB's fixed-universe scrambled draw.
- **Alternative: a separate generator class mirroring Java**: rejected; a class move was tried at the user's request on 2026-11-16 and rolled back. Instead, on 2026-11-17 the user chose a `Random` extension method (see "Extension method").

### Extension method (user decision, 2026-11-17)
The draw is exposed as a one-line call in the style of NBomber's `random.Zipf(n, s)`:

```csharp
public string GetKeyZipf(Random random)
{
    var keyNum = random.ScrambledZipf(_zipfianItemCount, LastAcknowledgedKey);
    return BuildKeyName(keyNum, _zeroPadding);
}
```

C# requires extension methods in a top-level static class, so a new file `Infra/ZipfianExtensions.cs` holds `public static class ZipfianExtensions` with:
- the Zipfian/FNV constants and `static readonly` values (moved unchanged from `DataGenerator`, UPPER_CASE, at the top);
- `public static ulong ScrambledZipf(this Random random, long itemCount, ulong maxKey)`: guards `itemCount >= 1` and `maxKey >= 1` (`ArgumentOutOfRangeException.ThrowIfLessThan`), then runs the redraw loop (`keyNum = 1 + FnvHash64(NextRank(random)) % itemCount` while `keyNum < 1 || (ulong)keyNum > maxKey`) and returns the key as `ulong`;
- private static `NextRank` and `FnvHash64` (moved unchanged).

`DataGenerator` keeps `GetZipfianItemCount` (workload logic, recordcount-0 guard, checked arithmetic), `_zipfianItemCount` and `ZipfianItemCount`, and reads `LastAcknowledgedKey` once per call by passing it as `maxKey`. Behaviour is identical. The `maxKey >= 1` guard turns misuse into an exception instead of an endless loop; `DataGenerator` never passes 0 (constructor guard).

### Hot-path cost
One-time: the constants and `static readonly` values (once per process) and `GetZipfianItemCount` (once per `DataGenerator`). Per zipfian key: one `NextDouble`, at most one `Math.Pow` (none for ranks 0 and 1), an 8-step FNV loop and one modulo, plus rare redraws. No harmonic sums, no zeta recomputation (Java also uses the precomputed `ZETAN`; Java recomputes `Math.pow(0.5, θ)` per draw, we precompute it). It runs before the measured step (T2), so it does not affect measured latency.

### Tests
**Decision (user, 2026-11-17): no tests in this change.** The tests below were written, run (82/82, later 69/69 after trimming) and then removed at the user's request; the list is kept as a record of what was verified.

- No direct `FnvHash64` test (it is private). Its Java values (fnv(0) = 6284781860667377211, re-derived by hand from `Utils.java`; fnv(1), fnv(2) from a port of the Java algorithm) are checked through key mapping.
- `ZipfianItemCount` theory: A, B, C, F → 1001; E → 1101 (recordcount 1000, operationcount 1000). D is not asserted.
- Large values: workload E, `RecordCount = 2147483647`, `OperationCount = 2147483647` → `ZipfianItemCount == 2362232012`; `RecordCount = ulong.MaxValue` → `ArgumentException` naming recordcount. Constructing `DataGenerator` does not generate the dataset, so these tests are cheap.
- Second item count: workload A, recordcount 1000000 (item count 1000001), `FixedRandom` U_RANK_0/1/2 → keys 801321, 216075, 971812.
- Small datasets (user requirement):
  - recordcount 1, workload A (item count 2, limit 1): `FixedRandom(U_RANK_0, U_RANK_1)` → key 1 with `Calls == 2` (rank 0 maps to key 2, redrawn); seeded 1,000 draws all key 1; after `GetKeyNext()` reserves key 2 without acknowledging, workload E with recordcount 1 (item count 102), seeded 1,000 draws are all key 1 and terminate.
  - recordcount 2, workload E (item count 103): with key 3 reserved and not acknowledged, seeded 2,000 draws are all 1 or 2 and both occur.
- Rank-to-key mapping with a scripted `Random`: a private nested `sealed class FixedRandom(params double[] values) : Random` overrides `NextDouble` to return the given values in order and exposes `Calls`. Constants `U_RANK_0 = 0.01` (u·ZETAN = 0.26 → rank 0), `U_RANK_1 = 0.04` (1.059 < 1.5035 → rank 1), `U_RANK_2 = 0.065` (Gray → rank 2; rank 2 covers u in [0.0568, 0.0721)). Workload A, recordcount 1000, ordered keys: keys 145, 611, 214.
- Deterministic redraw: workload A, recordcount 3 (item count 4, limit 3), ordered: script [U_RANK_0, U_RANK_1] → rank 0 maps to key 4 (redrawn), rank 1 maps to key 1 → returns key 1 with `Calls == 2`. After `AcknowledgeInsert(GetKeyNext().KeyNum)` (limit 4), script [U_RANK_0] → key 4.
- Empty dataset: constructing with `RecordCount = 0` throws `ArgumentException` (message contains `recordcount`) for A, B, C, E, F and does not throw for D.
- Shape (seeded, 200,000 draws, workload A, recordcount 1000): all keys in 1..1000, hottest share in [0.03, 0.05], top-10 share in [0.10, 0.17]. No assertion on which key is hottest or second (expected ≈3.8% for key 145, ≈12.7% for the top 10; tolerances are about 20 standard deviations wide).
- E growth (seeded): workload E, recordcount 1000, operationcount 1000: 20,000 draws all ≤ 1000; after reserving and acknowledging keys 1001..1100, 20,000 more draws include a key above 1000 and none above 1100.
- The existing T3 test `GetKeyZipf_Never_Returns_Unacknowledged_Key` stays unchanged (item count 3 at recordcount 2, as in YCSB).

### Documentation
`CLAUDE.md` Gotchas: in the known-deviations line, replace "Zipf constant 1.3 instead of scrambled 0.99" with "read latest uses NBomber `Zipf(n, 0.99)` rather than YCSB's Gray `ZipfianGenerator`"; keep "xxHash instead of FNV" for key names. Add a note that read/update/scan/RMW use YCSB's scrambled zipfian 0.99, that recordcount 0 is rejected for zipfian workloads, and that results before this change are not directly comparable.

### Follow-up (separate change, not in this task): read latest (workload D)
Differences between our `GetKeyLatest` (`Zipf(min(max, int.MaxValue), 0.99)`, key = max − (rank − 1)) and YCSB's `SkewedLatestGenerator` + `ZipfianGenerator`:
- Algorithm: YCSB uses the Gray approximation with exact ζ(n) updated incrementally (O(Δ) `Math.pow` under `synchronized`) as the limit grows; NBomber's `Zipf` algorithm is undocumented (exact or approximate unknown).
- YCSB recomputes eta with the constructor's `items`, not the current item count (a Java quirk we do not copy); YCSB effectively never chooses 0-based key 0, ours can choose key 1.
- Cost: if NBomber computes or caches a harmonic sum per n, the cache misses after every insert because n changes. Needs inspection (ILSpy) or a micro-benchmark. This cost is outside the measured step (affects throughput, not latency).
- `max <= 1 → key 1` is the same kind of fallback rejected here for zipfian; handle it in the follow-up.

### Destructive operations
None. Tests are offline. The empty-dataset guard throws before any database operation.

## Risks / Trade-offs

- [Results change for A, B, C, E, F on every database] → Intended (YCSB parity); documented; earlier results are not directly comparable.
- [Keys above `itemcount` are never chosen by zipfian, even after they are inserted] → Same as YCSB; read latest still reaches the newest keys.
- [`recordcount` 0] → Rejected at construction for A, B, C, E, F with a clear exception; D unchanged. [`operationcount` ≤ 0] → expected new keys clamped at 0 until T1.
- [E with a tiny recordcount and a huge operationcount] → Many redraws at the start, as in YCSB.
- [Floating-point differences from Java] → Same formulas in double precision; `Math.Pow` may differ by an ulp, so draw parity is statistical. Tests check the FNV hash with exact Java values, the exact rank-to-key mapping with a scripted `Random`, and the distribution shape with wide tolerances.
- [No JVM here] → `fnv(0)` and the rank probabilities were verified from the Java source by hand; the remaining FNV vectors and the hot-key order come from a port of the Java algorithm. An optional `jshell` check of `site.ycsb.Utils.fnvhash64` and a 10^6-draw Java histogram (0-based hot keys 144, 610, 213) can be run on a machine with a JVM.

## Migration Plan

No migration. Rollback is reverting the change.
