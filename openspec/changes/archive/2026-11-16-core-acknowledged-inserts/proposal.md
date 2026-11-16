# Proposal

## Why

In workloads D (95% read latest / 5% insert) and E (95% scan / 5% insert), reads can pick keys whose insert has not finished. `DataGenerator.GetKeyNext` reserves the next key with `Interlocked.Increment(ref _recordCount)`, and the same `_recordCount` is the upper bound for `GetKeyLatest` (read latest) and `GetKeyZipf` (read, update, scan, read-modify-write). So as soon as an insert reserves key N, before its database call completes, other scenario copies can read key N and get "no data".

In workload D this hits the hottest key: read latest picks the newest key with probability 1/H(n, 0.99), about 13% of reads at 1000 records. With `threadcount > 1` and inserts in flight, a share of D's reads become fast misses, which lowers latency and raises throughput. Because insert latency differs per database, the miss rate differs too, so cross-database comparisons are skewed. With `threadcount 1` there is no effect (the insert finishes before the next operation). In E, new keys are the coldest zipfian ranks, so the effect is negligible.

Original YCSB prevents this with `AcknowledgedCounterGenerator` (`generator/AcknowledgedCounterGenerator.java`): `doTransactionInsert` reserves a key with `nextValue()` and calls `acknowledge(keynum)` in `finally` after the insert; `lastValue()` returns the highest key such that it and every key below it are acknowledged. `nextKeynum()` retries any draw above `lastValue()`, and `SkewedLatestGenerator` (read latest) uses `lastValue()` as its maximum (`CoreWorkload.java` `nextKeynum`, `doTransactionInsert`; `SkewedLatestGenerator.nextValue`).

## What Changes

- Separate the two counters in the shared Core: the **reserved** insert key (next key for an insert) and the **acknowledged** key limit (highest key with every key up to it finished).
- An insert reserves its key before the measured step and acknowledges it after the step completes, whether the insert succeeded or failed (as in YCSB).
- Acknowledged keys advance the limit only when contiguous: an insert that finishes early does not expose a lower key that is still in flight.
- Read, read latest, update, scan and read-modify-write choose keys only up to the acknowledged limit.
- The key choosers take a `Random` (NBomber's per-copy `context.Random` is passed in, as already done for scan length) so they can be unit tested; key distributions are unchanged.
- Offline unit tests for reservation, out-of-order acknowledgement, the limit used by key choice, and concurrent use.
- Affected databases: **all**, through the shared Core. No database client changes; `IDbYcsbClient` is unchanged.
- **Out of scope**:
  - Zipfian constant and scrambling (keys still use NBomber `Zipf(n, 1.3)` for zipfian and `Zipf(n, 0.99)` for latest; backlog T6). YCSB's zipfian range of `recordcount + expected new keys` with retries above the limit belongs with T6.
  - Core option validation (T1), warm-up (T5), init result checks (T4), chunked loading (T7).
  - Database-specific scan semantics.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `core-workload`: adds requirements that reads only target acknowledged inserts, that insert keys are acknowledged after the insert regardless of outcome, and that the acknowledged limit advances only over contiguous keys. Existing requirements are unchanged.

## Impact

- Code: `src/NBomber.YCSB/Infra/DataGenerator.cs` (separate reserved and acknowledged counters, acknowledgement method, key choosers take `Random`), `src/NBomber.YCSB/YcsbScenario.cs` (insert case acknowledges in `finally`; key choosers receive `context.Random`). Tests: `tests/NBomber.YCSB.Tests/DataGeneratorTests.cs` (existing parallel key test adjusted to the new insert key API) and new acknowledgement tests.
- Benchmark results: with `threadcount > 1`, workload D reports fewer misses ("no data") and its read latencies reflect real reads; E changes negligibly; A, B, C, F have no inserts and are unchanged. Results of D before and after are not directly comparable.
- The live `Workload_D` tests count Ok + Fail together, so they keep passing and cannot observe the fix; a live run that checks the fail count needs explicit approval.
