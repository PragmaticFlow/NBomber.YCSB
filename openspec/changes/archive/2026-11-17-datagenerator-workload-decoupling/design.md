# Design

## Context

`YcsbScenario.Run` (line ~12) calls `WorkloadManager.GetOperations(settings.Workload)` for operation selection, then `new DataGenerator(settings)`. `DataGenerator` calls `GetOperations` again in two field initialisers (`_zipfianItemCount = GetZipfianItemCount(settings)`, `_latest = CreateLatestZipfian(settings)`).

## Goals / Non-Goals

- Goals: `DataGenerator` has no reference to `WorkloadManager` or `OperationType`; `GetOperations` called once; identical key behaviour.
- Non-goals: new types, defaults or flags; changes to `YcsbCliArgs.GetMaxScanLength`; algorithm changes.

## Decisions

### YcsbScenario
```csharp
var operations = WorkloadManager.GetOperations(settings.Workload);
var insertProportion = (double)operations.Where(o => o.Item1 == OperationType.Insert).Sum(o => o.Item2)
                       / operations.Sum(o => o.Item2);

var dataGen = new DataGenerator(settings, insertProportion);
```
The insert-proportion expression is moved unchanged, so the double value and therefore `expectedNewKeys` and the item count are bit-identical.

### DataGenerator (user decision, 2026-11-17)
The approved plan passed a third value, `usesReadLatest`. The user simplified it before commit: the read-latest state is created for every workload, so `DataGenerator` needs no read-latest flag.
- Primary constructor `DataGenerator(YcsbCliArgs settings, double insertProportion)`; `insertProportion` required.
- Field initialisers in the same order: `_zipfianItemCount = GetZipfianItemCount(settings, insertProportion)` (guard runs first), then `_latest = CreateLatestZipfian(settings)` (always, eagerly, before NBomber starts).
- `GetZipfianItemCount(YcsbCliArgs settings, double insertProportion)`:
  - `if (settings.RecordCount == 0) throw new ArgumentException("recordcount must be at least 1: keys are chosen from loaded records.", nameof(settings));`
  - `expectedNewKeys = Math.Max(0, (long)(settings.OperationCount * insertProportion * 2.0))`; checked sum and overflow message unchanged.
- `CreateLatestZipfian(YcsbCliArgs settings)`: the operation check is removed; the rest unchanged.
- `GetKeyLatest` null message: `"read latest was not enabled for this DataGenerator."`. With `_latest` always created, this path and the `_latest != null` check in `AcknowledgeInsert` can no longer trigger; they are kept as they are (not removed in this change).
- `AcknowledgeInsert`, snapshot, `GetKeyZipf`, `GetKeyLatest` draw: unchanged.

### Tests
- `CreateOrdered(ulong recordCount = 1000)` builds `new YcsbCliArgs { InsertOrder = "ordered", RecordCount = recordCount }` and passes `insertProportion: 0`. 0 is used for read-latest tests too: they do not observe the zipfian item count, and the test helper must not copy a workload's operation mix.
- Other call sites: `new DataGenerator(settings, insertProportion: 0)` (named argument).
- `Constructor_Rejects_RecordCount_Zero_For_Workload_D` still asserts `ArgumentException` with "recordcount".

### Hot path / destructive operations
No change to per-operation key choice. No database operations.

## Risks / Trade-offs

- [ζ built for every workload] → A, B, C, E, F now also compute ζ(recordcount) once at creation (~35 ms at 10^6 records, seconds at 10^8), before NBomber starts, so not measured. In workload E every acknowledged insert also extends ζ by one `Math.Pow` under the existing `_ackLock`, outside the measured step. Keys and item counts are unchanged.
- [A caller passes a value that does not match the workload] → Only `YcsbScenario` constructs `DataGenerator` in production, from the same `operations` it uses for selection.
- [Unconditional recordcount-0 guard] → Equivalent today (every workload A-F reads existing keys). A future insert-only (load-only) mix, valid with recordcount 0 in YCSB, would be rejected; that is the price of removing workload knowledge from `DataGenerator`. Revisit with T9 (custom operation mixes); no flag now (YAGNI).
- [recordcount 0 message is less specific] → Same exception type, parameter and the word recordcount; all workloads reject 0 as before.

## Migration Plan

None. Rollback is reverting the change.
