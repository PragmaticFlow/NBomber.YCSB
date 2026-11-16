# Tasks

## 1. Option and validation (Infra)

- [x] 1.1 In `src/NBomber.YCSB/Infra/YcsbCliArgs.cs`, add `WarmUp` (int, default 0) and `--warmup` (`DefaultValueFactory = _ => 0`, description from design.md); register it in `AddOptionsTo` and map it in `FromParseResult`
- [x] 1.2 In `src/NBomber.YCSB/Infra/YcsbCliArgsValidator.cs`, add the `WarmUp >= 0` rule with the message from design.md

## 2. Scenario

- [x] 2.1 In `src/NBomber.YCSB/YcsbScenario.cs`, remove `.WithWarmUpDuration(TimeSpan.FromSeconds(3))` from the builder chain and apply the `WarmUp > 0 ? WithWarmUpDuration(...) : WithoutWarmUp()` selection from design.md right after the chain; no other changes

## 3. Tests

- No new test file (user decision at plan approval, 2026-11-16: "without creating WarmUpTests"). Warm-up behaviour was verified by the pre-plan fake-client spike; this change is verified by build, the existing offline tests and code review.

## 4. Documentation

- [x] 4.1 `README.md`: add a short `## Warm-up` section (4-5 lines) before `## Links` with the content from design.md, including that results produced before this change used a fixed 3-second warm-up and may not be directly comparable, and that `--warmup 3` restores it; do not add any other usage documentation
- [x] 4.2 `CLAUDE.md`: add the warm-up clause to the `YcsbScenario` Architecture bullet, add the comparability note under Gotchas, and add `DataGeneratorTests` (already offline) to the offline test filter command, as in design.md

## 5. Build and regression

- [x] 5.1 Run `dotnet build NBomber.YCSB.sln`; verify 0 errors and no new warnings in touched files
- [x] 5.2 Run the offline tests (`dotnet test tests/NBomber.YCSB.Tests --no-build --filter "FullyQualifiedName~DataGeneratorTests|FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"`); verify all pass. No live-database test is required: database clients are untouched

## Workflow follow-up

- Architect review and user approval before implementation (backend-task Stage 2).
- Optional, requires explicit approval: run one live workload test class against the project's local container to confirm it still passes and is faster.
- Archive only after explicit user approval.
