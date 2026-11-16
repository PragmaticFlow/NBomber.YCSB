# Design

## Context

See proposal.md - Why, including the offline spike results. Facts used here:
- `YcsbScenario.Run` builds one scenario: `Scenario.Create(...).WithInit(...).WithWarmUpDuration(TimeSpan.FromSeconds(3)).WithLoadSimulations(Simulation.IterationsForConstant(copies: settings.ThreadCount, iterations: settings.OperationCount))`.
- NBomber: init runs before warm-up; when no warm-up simulations are set, warm-up reuses the load simulations capped by the warm-up duration (`NBomber.Contracts.xml`, `ScenarioProps.WarmUpSimulations`); warm-up statistics are excluded from final reports (`OperationType.WarmUp`); the default warm-up is 30 s (`NBomber.xml`, `Scenario.WithWarmUpDuration`), so omitting the call does not disable warm-up; `Scenario.WithoutWarmUp()` disables it.
- `WithoutWarmUp()` and `WithWarmUpDuration(TimeSpan)` are C# extension methods on `ScenarioProps` (`NBomber.CSharp.Scenario`) returning a new `ScenarioProps`; warm-up reads `LoadSimulations` at run time, so their position relative to `WithLoadSimulations` does not matter. There is no C# builder for warm-up simulations.
- Core settings are typed options in `YcsbCliArgs` (property + `Option<T>` with `DefaultValueFactory`, registered in `AddOptionsTo`, mapped in `FromParseResult`), validated by `YcsbCliArgsValidator` (FluentValidation, lowercase option names in messages). Live tests build `YcsbCliArgs` directly.
- Requirements are in `specs/core-workload/spec.md`.

## Goals / Non-Goals

**Goals:**
- Default run = YCSB: no warm-up, database receives exactly `operationcount` workload operations.
- Opt-in `--warmup <seconds>`; `--warmup 3` = previous behaviour.
- Reuse the existing option, validator and scenario code; no new classes in `src`.
- Offline proof with a fake client.

**Non-Goals:**
- Database `InitDb` warm-ups, warm-up by operation count, other validation, exit codes.

## Decisions

### Option in `YcsbCliArgs`
- `public int WarmUp { get; set; } = 0;` (seconds), next to the other Core settings.
- `private static readonly Option<int> WarmUpOpt = new("--warmup") { Description = "Warm-up duration in seconds before measurement; warm-up operations run against the database but are not measured (default: 0, as in YCSB)", DefaultValueFactory = _ => 0 };`
- Register in `AddOptionsTo`, map in `FromParseResult` (`WarmUp = p.GetValue(WarmUpOpt)`).
- Whole seconds as `int`, like the other numeric options; no class constant is needed because the existing defaults are inline literals in the same style.

### Validation in `YcsbCliArgsValidator`
`RuleFor(x => x.WarmUp).GreaterThanOrEqualTo(0).WithMessage(x => $"warmup ({x.WarmUp}) must be 0 or greater.")`.

### Warm-up selection in `YcsbScenario`
Remove `.WithWarmUpDuration(TimeSpan.FromSeconds(3))` from the builder chain and, right after the chain, apply:

```csharp
scenario = settings.WarmUp > 0
    ? scenario.WithWarmUpDuration(TimeSpan.FromSeconds(settings.WarmUp))
    : scenario.WithoutWarmUp();
```

- `WithoutWarmUp()` for 0 because NBomber's default is a 30 s warm-up; `WithWarmUpDuration(TimeSpan.Zero)` is not documented as disabling it.
- `--warmup 3` produces exactly the previous `.WithWarmUpDuration(TimeSpan.FromSeconds(3))`.
- Warm-up inserts keep going through `GetKeyNext`/`AcknowledgeInsert`, so keys stay unique and the acknowledged limit stays correct when warm-up is on.
- **Alternative: remove warm-up entirely (`WithoutWarmUp()` only)**: rejected by the user; opt-in warm-up remains useful for steady-state runs.
- **Alternative: warm-up by operation count**: rejected; NBomber's C# API has no builder for warm-up simulations, so it would need custom code.

### Documentation
- `README.md`: a short `## Warm-up` section (4-5 lines) placed before `## Links` (the README has no usage section; do not add one): default no warm-up (as YCSB); `--warmup <seconds>` opt-in; warm-up operations are extra, unmeasured and change data, and because warm-up is time-capped a faster database runs more of them; results produced before this change used a fixed 3-second warm-up and may not be directly comparable; `--warmup 3` reproduces the old behaviour.
- `CLAUDE.md`: in the Architecture bullet for `YcsbScenario`, add "no warm-up unless `--warmup <seconds>`; warm-up operations are extra, unmeasured and change data"; add the comparability note under Gotchas; add `DataGeneratorTests` (already offline, missing from the documented filter) to the offline test filter command.

### Tests
**Decision (user, 2026-11-16): no `WarmUpTests` file is created in this change.** The behaviour the tests below would cover was measured by the pre-plan fake-client spike (proposal.md - Why); the change is verified by build, the existing offline tests and code review. The list below is kept as a reference for a possible later test change and is not implemented now.

Reference only (not implemented):
- Parsing and validation in `ScanLengthTests` style: default `WarmUp == 0` (direct construction and `run --workload A` parse); `--warmup 3` parses to 3; validator accepts 0 and 3 and rejects -1 with a message containing `warmup`.
- Scenario tests with a private call-counting fake `IDbYcsbClient` (reuse the spike's fake: counts calls per method with `Interlocked`, records insert keys in a `ConcurrentBag<string>`, returns `Response.Ok(sizeBytes: 0)`), `RecordCount = 100`, `ThreadCount = 1`:
  - "Fake operation calls" = calls to the five workload methods only (Insert, Update, Read, Scan, ReadModifyWrite), not InitDb/DeleteAllData/BulkInsert. "Measured count" = the sum of `Ok.Request.Count + Fail.Request.Count` over `ScenarioStats[0].StepStats` of the returned `NodeStats` (the pattern the live tests use).
  - In every scenario test, assert InitDb, DeleteAllData and BulkInsert were each called exactly once.
  - `WarmUp = 0`, workload A, `OperationCount = 200`: fake operation calls == 200 and measured count == 200;
  - `WarmUp = 1`, workload A, `OperationCount = 200`: measured count == 200 and `200 < fake operation calls <= 400` (warm-up is capped by the iteration count for a fast fake);
  - `WarmUp = 0`, workload D, `OperationCount = 200`: fake Insert calls == measured `insert` step count (no unmeasured inserts);
  - `WarmUp = 1`, workload D, `OperationCount = 1000`: fake Insert calls > measured `insert` count, and all recorded insert keys are distinct (1000 operations make zero warm-up inserts at 5% practically impossible).
- These tests run NBomber in-process (about 1-2 s each, plus 1 s per warm-up test) and need no database. xUnit runs them serially within the class; NBomber writes `reports/` into the test bin directory (ignored by git), as the live tests do.

### Hot-path cost
None.

### Destructive operations
None. Tests use a fake client.

## Risks / Trade-offs

- [Default results change: fewer operations reach the database and the first measured operations include JIT/connection setup] → Intended (YCSB parity); documented; `--warmup 3` restores the old behaviour.
- [Warm-up > 0 still changes data and grows D/E datasets before measurement] → Documented in the option description, README and spec.
- [Scenario tests run NBomber in-process and may create a reports folder in the test output] → Same as the live tests; outputs are ignored by git.
- [Offline tests are not run by CI] → Pre-existing; the new class is added to the documented offline filter.

## Migration Plan

No data migration. Users who want the previous behaviour pass `--warmup 3`. Rollback is reverting the change.
