# Tasks

## 1. Implementation (Core)

- [x] 1.1 `src/NBomber.YCSB/YcsbScenario.cs`: compute `insertProportion` from the existing `operations` and pass it to `new DataGenerator(settings, insertProportion)` as in design.md (user simplified: no `usesReadLatest`)
- [x] 1.2 `src/NBomber.YCSB/Infra/DataGenerator.cs`: constructor `(YcsbCliArgs settings, double insertProportion)`; `GetZipfianItemCount(settings, insertProportion)` with the unconditional recordcount-0 guard; `_latest` created for every workload; `CreateLatestZipfian` without the operation check; `GetKeyLatest` null message as in design.md; remove every `WorkloadManager`/`OperationType` reference; update the `GetKeyZipf` and `_latest` comments; no `settings` reference remains outside field initialisers

## 2. Tests (offline)

- [x] 2.1 Update constructor calls in `tests/NBomber.YCSB.Tests/DataGeneratorTests.cs` (`CreateOrdered(recordCount)`, direct `new DataGenerator(..., insertProportion: 0)` calls) and `ScanLengthTests.cs` as in design.md; no new tests, assertions unchanged

## 3. Build and regression

- [x] 3.1 Run `dotnet build NBomber.YCSB.sln`; 0 errors, no new warnings in touched files
- [x] 3.2 Run the offline tests (`DataGeneratorTests`, `ScanLengthTests`, `RedisHelperTests`, `ScyllaDbHelperTests`, `ZipfianExtensionsTests`); all pass
- [x] 3.3 `grep` confirms no `WorkloadManager`, `OperationType` or `settings.Workload` in `DataGenerator.cs`

## 4. Live check

- [ ] 4.1 Optional, **requires explicit approval to run**: one workload live test class (e.g. Scylla, container already running) with an exact filter
