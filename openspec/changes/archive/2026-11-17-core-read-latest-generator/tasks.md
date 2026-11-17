# Tasks

## 1. Read latest generator (Infra)

- [x] 1.1 In `src/NBomber.YCSB/Infra/ZipfianExtensions.cs`, generalise `NextRank` to take item count, ζ(n) and eta; `ScrambledZipf` passes its existing constants (behaviour unchanged); add `Zipfian`, `ZipfianEta` and `ZipfianZeta` as in design.md (ζ terms as `1 / Math.Pow(k, ZIPFIAN_CONSTANT)`, ascending k)
- [x] 1.2 In `src/NBomber.YCSB/Infra/DataGenerator.cs`, add the `LatestZipfian` snapshot (created in the constructor only for workloads with read latest); extend it in `AcknowledgeInsert` under `_ackLock` after the `Interlocked.Exchange` (no new lock); rewrite `GetKeyLatest(Random random)` lock-free as in design.md (throw `InvalidOperationException` when `_latest` is null); remove the `theta` parameter, the `max <= 1` branch, the `int.MaxValue` cap and `using NBomber.CSharp;`
- [x] 1.3 In `GetZipfianItemCount`, include `ReadLatest` in the recordcount-0 guard

## 2. Tests (offline)

- [x] 2.1 `GetKeyLatest_Never_Returns_Unacknowledged_Key`: set `Workload = Workload.D` (the default workload A builds no read-latest state); otherwise unchanged
- [x] 2.2 New seeded tests in `DataGeneratorTests` (workload D, ordered inserts, wide tolerances, no timing assertions):
  - shape: recordcount 1000, all keys in 1..1000, key 1000 is the most frequent with a share in [0.11, 0.15], key 999 in [0.055, 0.075]
  - growth: after reserving and acknowledging key 1001, key 1001 is the most frequent and no key above 1001 is chosen
  - incremental equals full: recordcount 1000 with keys 1001..2000 acknowledged gives the same key sequence (same seed) as recordcount 2000
  - small datasets: recordcount 1 always returns key 1 (also with key 2 reserved, not acknowledged); recordcount 2 returns only keys 1 and 2, key 2 share in [0.60, 0.73]
  - recordcount 0 with workload D is rejected with `ArgumentException` naming recordcount

## 3. Documentation

- [x] 3.1 `CLAUDE.md` Gotchas: remove "read latest uses NBomber `Zipf(n, 0.99)` rather than YCSB's Gray `ZipfianGenerator`" from the deviations line; change "`recordcount 0` is rejected for the zipfian workloads (A, B, C, E, F)" to all workloads (A-F); note that D results before this change are not directly comparable

## 4. Build and regression

- [x] 4.1 Run `dotnet build NBomber.YCSB.sln`; 0 errors, no new warnings in touched files
- [x] 4.2 Run the offline tests (`DataGeneratorTests`, `ScanLengthTests`, `RedisHelperTests`, `ScyllaDbHelperTests`, `ZipfianExtensionsTests`); all pass, including the unchanged `ZipfianExtensionsTests` (scrambled zipfian unchanged)

## 5. Live-database tests

- [ ] 5.1 Optional, **requires explicit approval to run**: one workload D live test (e.g. Redis) against the project's own local container, exact `FullyQualifiedName=` filter
