# Design

## Context

See proposal.md - Why. `YcsbScenario.Run` builds one NBomber scenario. Each iteration picks an operation with `context.Random.Choice(operations)` and runs a `switch` whose cases call `Step.Run("<name>", context, () => { ...prepare inputs...; return ycsbClient.X(...); })`. NBomber measures a step's latency from the start to the end of the `Step.Run` lambda, so everything inside it is timed. The client methods are async and return `Task<Response<object>>`; preparation code inside the lambda runs synchronously before the first `await` and is therefore counted. This applies to the per-step statistics; NBomber's scenario-level statistics cover the whole iteration and keep including generation (as Java YCSB's overall throughput is computed over wall time).

`DataGenerator` (shared by all scenario copies) provides `GetKeyNext` (Interlocked counter), `GetKeyZipf(context)` / `GetKeyLatest(context)` (NBomber's per-scenario `context.Random`), `GetFieldNames`, `CreateValues`, `CreateValuesToUpdate`, `NextScanLength(Random)` and `GenerateRandoms` (initial data, outside the measured phase). Values and single-field indexes come from `ThreadLocal<Faker>`; all those Fakers share Bogus's global seeded `Random`. Bogus is referenced only by `DataGenerator`.

Requirements are in `specs/core-workload/spec.md`.

## Goals / Non-Goals

**Goals:**
- Only the database call is inside each `Step.Run`.
- No shared lock in data generation; Bogus removed.
- Generated data, step names, operation mix and key choice unchanged.
- No new classes besides one test file; `IDbYcsbClient` unchanged.

**Non-Goals:**
- Acknowledged insert counter (T3), zipfian changes, warm-up, key hash, naming, alphabet, chunked loading, validation.

## Decisions

### Prepare inputs before `Step.Run` (`YcsbScenario`)
Move every `dataGen.*` call out of the step lambda into the `case` body, and leave only the client call inside. Wrap each `case` body in braces so the local names (`key`, `fields`, `values`) can repeat across cases. Example:

```csharp
case OperationType.Insert:
{
    var key = dataGen.GetKeyNext();
    var values = dataGen.CreateValues();

    await Step.Run("insert", context, () => ycsbClient.Insert(table: tableName, key, values));
    break;
}
```

The read-modify-write lambda becomes `() => ycsbClient.ReadModifyWrite(table: tableName, key, fields, updateValues)` (no extra `async`/`await` wrapper). Step names stay exactly `insert`, `read`, `read latest`, `update`, `scan`, `read-modify-write`. The order of calls inside each case is unchanged, so key choice and randomness consumption are as before.

- **Insert key**: `GetKeyNext()` now runs just before the insert step instead of at its start; the gap is only the value generation. This keeps the "reserve key, then run step" shape that the acknowledged counter (T3) needs; it does not by itself change the read-latest behaviour.
- **Alternative: keep generation inside the step but make it faster**: rejected; any client-side work inside the step is still measured, unlike YCSB.

### Replace Bogus with `Random.Shared` (`DataGenerator`)
- Remove `using Bogus;` and the `_faker` field.
- Single field index: `Random.Shared.Next(1, _fieldCount + 1)` in `CreateValuesToUpdate` and `GetFieldNames`. The upper bound of `Next` is exclusive, so `+ 1` keeps `fieldN` reachable (Bogus `Int(1, n)` was inclusive).
- Values:

```csharp
// first member of the class
private const string ALPHABET = "abcdefghijklmnopqrstuvwxyz";
...
private string GenerateFieldValue()
{
    return string.Create(_fieldLength, 0, static (chars, _) =>
    {
        for (var i = 0; i < chars.Length; i++)
            chars[i] = ALPHABET[Random.Shared.Next(ALPHABET.Length)];
    });
}
```

(2026-11-16, user: constants sit at the top of the class and use upper-case names.)

`Random.Shared` is thread-safe and uses per-thread state internally, so concurrent copies do not share a lock, and it works regardless of which thread-pool thread a copy resumes on. `string.Create` writes directly into the result string.

- **Alternative: keep Bogus with a per-thread local seed (`new Faker { Random = new Randomizer(seed) }`)**: rejected; it removes the shared seed but not the shared lock, because every `Randomizer` takes the static `Randomizer.Locker` (from Bogus source), and it keeps a dependency used for two trivial calls plus the per-character overhead.
- **Alternative: use NBomber's `context.Random` for values**: rejected; `GenerateRandoms` runs in `WithInit`, where there is no per-copy context, and threading a `Random` through `CreateValues`/`CreateValuesToUpdate`/`GetFieldNames` changes more signatures for no benefit.
- Key choice keeps using `context.Random` (unchanged).

### Remove the package (`NBomber.YCSB.csproj`)
Delete `<PackageReference Include="Bogus" Version="35.6.5" />`. The test project does not reference Bogus. No package is added.

### Hot-path cost
Lower: generation leaves the measured step and no longer takes Bogus's global lock. The total client CPU per operation is the same or lower. No change to allocations beyond removing Bogus's own.

### Destructive operations
None. Tests are offline; live workload tests are optional and need explicit approval.

## Risks / Trade-offs

- [Measured latency and throughput change for every database] → Intended; earlier results are not directly comparable. Note it in the change summary.
- [`Next` exclusive upper bound mistake would make fieldN unreachable] → Covered by a test that requires both field1 and fieldN to be chosen.
- [Values are no longer reproducible via `Randomizer.Seed`] → Nothing in the repository sets it; YCSB values are not seeded either.
- ["Inputs prepared before timing" cannot be unit-tested without running NBomber] → Verified by code review: each `Step.Run` lambda contains only the client call.

## Migration Plan

No migration. Command lines and data shape are unchanged. Rollback is reverting the change (and restoring the Bogus reference).
