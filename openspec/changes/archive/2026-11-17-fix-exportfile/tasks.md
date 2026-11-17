# Tasks

## 1. Implementation (Core CLI / scenario)

- [x] 1.1 `src/NBomber.YCSB/Infra/YcsbCliArgsValidator.cs`: replace the `DirectoryExists` rule with `.Must(IsValidExportFile)` as in design.md (file name not null/whitespace, no invalid file-name chars, folder without invalid path chars; lambda message naming exportfile); remove `DirectoryExists`
- [x] 1.2 `src/NBomber.YCSB/YcsbScenario.cs`: guard with `!string.IsNullOrWhiteSpace`, split `ExportFile` into `WithReportFolder` (only for a non-empty folder) and `WithReportFileName(Path.GetFileName(...))` as in design.md

## 2. Tests (offline)

- No tests in this change (user decision, 2026-11-17: "remove YcsbCliArgsValidatorTests at all"). A `YcsbCliArgsValidatorTests.cs` with 6 valid and 5 invalid values was written and passed (82/82), then deleted. The existing validator tests in `ScanLengthTests` are unchanged.

## 3. Build, regression, docs

- [x] 3.1 Run `dotnet build NBomber.YCSB.sln`; 0 errors, no new warnings in touched files
- [x] 3.2 Run the offline tests with `FullyQualifiedName~DataGeneratorTests|FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests|FullyQualifiedName~ZipfianExtensionsTests`; all pass
- [x] 3.3 `CLAUDE.md`: add `ZipfianExtensionsTests` to the offline test command; add a one-line Gotcha: `--exportfile dir/name` writes `dir/name.{csv,html,md,txt}` without a timestamp folder (repeated runs overwrite), a bare name goes to `reports/<timestamp>/`

## 4. Live check

- [ ] 4.1 Optional, **requires explicit approval to run**: one short run with `--exportfile <folder>/<name>` against a project-local container (destructive cleanup of that container's data), confirm the report files appear in the folder
