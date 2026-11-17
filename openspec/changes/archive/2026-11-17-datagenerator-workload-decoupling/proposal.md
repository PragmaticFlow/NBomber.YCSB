# Proposal

## Why

`DataGenerator` reads the workload definition itself: `GetZipfianItemCount` and `CreateLatestZipfian` call `WorkloadManager.GetOperations(settings.Workload)` and inspect `OperationType` to get the insert share and to decide whether read-latest state is needed. `YcsbScenario.Run` already calls `GetOperations` for the same workload and owns operation selection.

The user requires a clear separation of responsibilities (explicit architectural requirement):
- `YcsbScenario` owns the workload configuration and every operation-related decision;
- `DataGenerator` generates data and keys from the values it is given and knows nothing about operations or workload definitions.

A database-client fix does not apply: this is shared Core code.

## What Changes

- `YcsbScenario.Run` computes, from the operations it already has, `insertProportion` (insert weight / total weight, same expression as today) and passes it to the `DataGenerator` constructor. `GetOperations` is called once per run.
- `DataGenerator` takes `(YcsbCliArgs settings, double insertProportion)` and no longer references `WorkloadManager` or `OperationType`:
  - `GetZipfianItemCount` uses the given insert proportion (same `expectedNewKeys` and item count arithmetic);
  - `CreateLatestZipfian` builds ζ eagerly in the constructor for every workload (user decision before commit; the approved plan passed a `usesReadLatest` flag);
  - the `recordcount 0` guard becomes unconditional (its condition was true for every workload A-F), still an `ArgumentException` naming recordcount, thrown in the constructor before the database is initialised.
- Test call sites pass the values explicitly.
- Affected databases: none directly (Core, all backends share it).
- **Out of scope**: `YcsbCliArgs.GetMaxScanLength` (reads the workload's default max scan length through `WorkloadManager`; `DataGenerator` only calls the `YcsbCliArgs` method); Zipfian algorithms; `AcknowledgeInsert` and snapshot logic; the open `fix-exportfile` change.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
<!-- None: no behaviour change (skip_specs). -->

## Impact

- Code: `src/NBomber.YCSB/YcsbScenario.cs`, `src/NBomber.YCSB/Infra/DataGenerator.cs`. Tests: constructor calls in `tests/NBomber.YCSB.Tests/DataGeneratorTests.cs` and `ScanLengthTests.cs`.
- **Benchmark results: no change.** Same item counts and same keys for the same seeds. ζ is now also built for A, B, C, E, F at creation (before NBomber starts, not measured), and workload E extends it by one `Math.Pow` per acknowledged insert under the existing lock, outside the measured step.
- The `recordcount 0` error message no longer lists operations (it cannot know them); type and parameter are unchanged.
