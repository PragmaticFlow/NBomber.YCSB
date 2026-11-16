# Proposal

## Why

Each operation's latency should measure only the database call. In original YCSB, `DBWrapper` times only the call to the database binding; keys, field sets and values are prepared before it (`CoreWorkload.doTransactionUpdate`, `doTransactionInsert`, `doTransactionRead`). In NBomber.YCSB, every operation prepares its inputs inside the timed `Step.Run` lambda (`YcsbScenario.cs` lines 25-84): key choice (`GetKeyZipf`, `GetKeyLatest`, `GetKeyNext`, including xxHash), field selection (`GetFieldNames`), value generation (`CreateValues`, `CreateValuesToUpdate`) and scan length (`NextScanLength`). At defaults an insert builds 10 values of 100 characters inside the measured time. This breaks the project rule "do not add work inside the timed operation" and inflates latency most for fast databases (Redis), which hurts cross-database comparability.

Value and field generation also use Bogus through `ThreadLocal<Faker>` (`DataGenerator.cs` line 18). `new Faker()` uses Bogus's global static `Randomizer.Seed` (Bogus.xml, `Randomizer.#ctor`), so all scenario copies draw from one shared `System.Random`. Every Bogus `Randomizer` draw also takes a static lock (`Randomizer.Locker`, shared by all instances, even locally seeded ones; from Bogus source, not documented in Bogus.xml), one draw per character. With `threadcount > 1` the copies contend for that lock. `ThreadLocal` is also a poor fit for async scenario copies, which can resume on different thread-pool threads. Bogus is used for nothing else in the repository.

## What Changes

- Every operation prepares its key, field set, values and scan length before the timed step starts; the timed step only calls the database client. Step names and the operation mix are unchanged.
- Value and single-field generation use .NET's thread-safe `Random.Shared` instead of Bogus. Generated data is unchanged: values are `fieldlength` lowercase letters `a`-`z`; single-field reads and updates choose uniformly from `field1..fieldN`; inserts and full updates write all `fieldcount` fields.
- Remove the `Bogus` package reference (one dependency fewer; nothing new is added).
- Offline unit tests for value length and alphabet, field selection range (both ends reachable), full-record field count, and concurrent generation.
- Affected databases: **all**, through the shared Core. No database client changes; `IDbYcsbClient` is unchanged.
- **Out of scope**:
  - Acknowledged insert counter / read-latest of unwritten keys (backlog T3; this change only keeps the insert key reservation outside the step, which T3 builds on).
  - Zipfian constant and scrambling, warm-up, key hash function, key/field naming, value alphabet (kept lowercase), chunked loading.
  - Core option validation (backlog T1).

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `core-workload`: adds requirements that operation latency covers only the database call, and that generated record data has the documented shape and is safe under concurrent scenario copies. Existing scan length requirements are unchanged.

## Impact

- Code: `src/NBomber.YCSB/YcsbScenario.cs` (prepare inputs before `Step.Run`), `src/NBomber.YCSB/Infra/DataGenerator.cs` (replace Bogus with `Random.Shared`), `src/NBomber.YCSB/NBomber.YCSB.csproj` (remove Bogus). Tests: new offline test file in `tests/NBomber.YCSB.Tests`.
- Benchmark results: measured per-operation latency drops by the client-side generation time, most visibly for inserts and for full updates; throughput may rise, especially with many threads. Results before and after this change are not directly comparable. Data shape and operation mix are unchanged. Only the per-step statistics (`insert`, `read`, ...) exclude generation; NBomber's scenario-level statistics still cover the whole iteration, like Java YCSB's overall throughput over wall time, so compare per-step latency.
- Initial data load (`GenerateRandoms`) also uses the new generator; it is outside the measured phase and becomes faster.
- The live `Workload_*` tests check only the operation mix and keep passing; running them needs explicit approval.
