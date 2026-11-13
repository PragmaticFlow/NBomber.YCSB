# Tasks

## 1. Settings and defaults (Infra)

- [x] 1.1 In `src/NBomber.YCSB/Infra/Workload.cs`, add `WorkloadManager.GetDefaultMaxScanLength(Workload workload)` returning 100 for `Workload.E` and 1000 otherwise, in the style of `GetDescription`; verify with unit tests in task 1.4
- [x] 1.2 In `src/NBomber.YCSB/Infra/YcsbCliArgs.cs`, add `MinScanLength` (int, default 1) with `--minscanlength` (`DefaultValueFactory = _ => 1`), `MaxScanLength` (int?, no default) with `--maxscanlength`, register both in `AddOptionsTo`, map both in `FromParseResult`, and add `GetMaxScanLength()` returning `MaxScanLength ?? WorkloadManager.GetDefaultMaxScanLength(Workload)`; verify with task 1.4
- [x] 1.3 In `src/NBomber.YCSB/Infra/YcsbCliArgsValidator.cs`, add rules `RuleFor(x => x.MinScanLength).GreaterThanOrEqualTo(1).WithMessage("minscanlength must be at least 1.")` and `RuleFor(x => x.MinScanLength).LessThanOrEqualTo(x => x.GetMaxScanLength()).WithMessage(x => $"minscanlength ({x.MinScanLength}) must not exceed maxscanlength ({x.GetMaxScanLength()}).")`; verify with task 1.4
- [x] 1.4 Add offline tests in a new `tests/NBomber.YCSB.Tests/ScanLengthTests.cs`: default max is 100 for E and 1000 for A, B, C, D, F; an explicit `MaxScanLength` overrides the default for E and for another workload; `MinScanLength` defaults to 1; the validator accepts defaults, rejects min 0, rejects min 50 / max 10, and rejects E with min 200 and no max; parsing `run --workload E` through `YcsbCliArgs.AddOptionsTo` + `FromParseResult` gives `MinScanLength == 1` and `MaxScanLength == null`, and `--maxscanlength 500` gives 500; verify all pass

## 2. Scan length generation (Infra and scenario)

- [x] 2.1 In `src/NBomber.YCSB/Infra/DataGenerator.cs`, capture `_minScanLength = settings.MinScanLength` and `_maxScanLength = settings.GetMaxScanLength()` and add `public int NextScanLength(Random random) => (int)random.NextInt64(_minScanLength, (long)_maxScanLength + 1);`; verify with task 2.3
- [x] 2.2 In `src/NBomber.YCSB/YcsbScenario.cs`, replace `context.Random.Next(1, 10)` in the scan step with `dataGen.NextScanLength(context.Random)`; verify by inspecting the diff (only that line changes in the scenario)
- [x] 2.3 Add offline tests to `ScanLengthTests.cs` using a seeded `Random`: with defaults for E, 100,000 draws all lie in [1, 100], both 1 and 100 occur, and the mean is 50.5 ± 1; with min = max = 10 every draw is 10; with an explicit max of 500 for E every draw lies in [1, 500]; with `MinScanLength = 20` and no max for E every draw lies in [20, 100]; with `MaxScanLength = int.MaxValue` the result lies in [1, int.MaxValue]; verify all pass

## 3. Build and regression

- [x] 3.1 Run `dotnet build NBomber.YCSB.sln`; verify 0 errors and no new warnings in the changed files
- [x] 3.2 Run the offline tests (`dotnet test tests/NBomber.YCSB.Tests --no-build --filter "FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"`); verify all pass. No live-database test is needed: the change is shared Core logic, and database clients are untouched

## Workflow follow-up

- Architect review and user approval before implementation (backend-task Stage 2).
- Optional, requires explicit approval to run: a workload E run against one local test database to observe the larger scans.
- Archive only after explicit user approval.
