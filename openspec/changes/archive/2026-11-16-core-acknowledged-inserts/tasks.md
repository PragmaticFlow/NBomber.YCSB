# Tasks

## 1. Acknowledged insert counter (Infra)

- [x] 1.1 In `src/NBomber.YCSB/Infra/DataGenerator.cs`, replace the mutable `_recordCount` with the fields from design.md (`readonly _recordCount`, `_ackLock`, `_pendingAcks`, `_lastInsertKey`, `_lastAcknowledgedKey`); remove `SetRecordCount`; keep `GenerateRandoms` looping `1.._recordCount`
- [x] 1.2 Change `GetKeyNext()` to return `(ulong KeyNum, string Key)` reserving with `Interlocked.Increment(ref _lastInsertKey)`; add `AcknowledgeInsert(ulong keyNum)` and `LastAcknowledgedKey` exactly as in design.md
- [x] 1.3 Change `GetKeyZipf(IScenarioContext)` to `GetKeyZipf(Random random)` and `GetKeyLatest(IScenarioContext, double)` to `GetKeyLatest(Random random, double theta = 0.99)`, using `random.Zipf(...)` and `LastAcknowledgedKey`; keep the rest of both bodies unchanged; remove `using NBomber.Contracts;` if unused

## 2. Scenario (insert acknowledgement)

- [x] 2.1 In `src/NBomber.YCSB/YcsbScenario.cs`, change the insert case to the shape in design.md (`CreateValues()` first, then `var (keyNum, key) = dataGen.GetKeyNext();` immediately before `try`, acknowledge in `finally`, no `catch`), remove the `dataGen.SetRecordCount(...)` call in `WithInit`, and pass `context.Random` to `GetKeyZipf`/`GetKeyLatest` in the other five cases; no other changes (step names, call order and `context.Random` consumption order unchanged)

## 3. Tests (offline)

- [x] 3.1 In `tests/NBomber.YCSB.Tests/DataGeneratorTests.cs`, update `GetKeyNext_Is_Unique_Under_Parallel_Use` to use `.Key`
- [x] 3.2 Add tests to `DataGeneratorTests.cs` (settings with `InsertOrder = "ordered"`, `RecordCount = 1000` unless stated; seeded choosers use `new Random(<const seed>)`; parse key numbers with `ulong.Parse(key["user".Length..])`; any new test constants go at the top of the class in UPPER_CASE):
  - a new generator has `LastAcknowledgedKey == 1000`;
  - out-of-order: reserve 1001, 1002, 1003; ack 1003 -> 1000; ack 1001 -> 1001; ack 1002 -> 1003;
  - acknowledging an already-covered key again (e.g. 1001 twice) leaves the limit unchanged;
  - with 1001 reserved and not acknowledged, 10,000 `GetKeyLatest` draws never return a key above 1000 and do return 1000; after acknowledging 1001, draws return 1001 and never above it;
  - with `RecordCount = 2`, key 3 reserved and not acknowledged, 10,000 `GetKeyZipf` draws are all 1 or 2; after acknowledging 3, key 3 appears;
  - `Parallel.For` of 10,000 iterations each reserving and then acknowledging a key yields exactly keys 1001..11000 once each and `LastAcknowledgedKey == 11000`;
  - verify all pass

## 4. Build and regression

- [x] 4.1 Run `dotnet build NBomber.YCSB.sln`; verify 0 errors and no new warnings in touched files
- [x] 4.2 Run the offline tests (`dotnet test tests/NBomber.YCSB.Tests --no-build --filter "FullyQualifiedName~DataGeneratorTests|FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"`); verify all pass. No live-database test is run: database clients are untouched

## Workflow follow-up

- Architect review and user approval before implementation (backend-task Stage 2).
- Optional, requires explicit approval: run workload D with threadcount 16 against the project's local Redis container and compare the read latest fail count before and after.
- Archive only after explicit user approval.
