# Proposal

## Why

`--exportfile` cannot produce a report today (backlog T11, gap G14):

- `YcsbCliArgsValidator.DirectoryExists` requires `Path.GetDirectoryName(value)` to be an existing directory. A bare file name such as `results` has an empty directory name, `Directory.Exists("")` is false, and the run is rejected with "The directory for ExportFile does not exist."
- `YcsbScenario` passes the whole value to NBomber `WithReportFileName`. A probe of NBomber 6.4.1 (scratch console) shows that NBomber rejects any name containing a path separator: `Run()` throws "Report file name contains invalid chars". So every value the validator accepts (`existing/name`, absolute paths) makes NBomber throw.

The validator therefore accepts exactly the values NBomber rejects, and rejects the only kind NBomber accepts.

NBomber behaviour (probe):

| Call | Result |
|---|---|
| `WithReportFileName("name")` | `name.{csv,html,md,txt}` in NBomber's default `reports/<timestamp>/` folder |
| `WithReportFileName("dir/name")` | throws "Report file name contains invalid chars" |
| `WithReportFolder("dir")` + `WithReportFileName("name")` | `dir/name.{csv,html,md,txt}`, no timestamp subfolder; missing folders are created |
| `WithReportFileName("results.json")` | `results.json.csv`, `results.json.html`, ... (the name is used as given) |

In YCSB, `exportfile` is a file path. A database-client fix is not possible: this is shared CLI and reporting code.

## What Changes

- `--exportfile <path>` is split into a folder and a file name:
  - the file name (`Path.GetFileName`) goes to `WithReportFileName`;
  - a non-empty folder (`Path.GetDirectoryName`) goes to `WithReportFolder`, so reports land in that folder (created if missing);
  - a bare file name keeps NBomber's default location `reports/<timestamp>/`.
- Validation checks what NBomber needs: the value must end in a file name (not a folder such as `out/`) and the file name must not contain invalid file-name characters. The "directory must exist" rule is removed, because NBomber creates the folder.
- The file name is used as given (no extension handling): NBomber appends one extension per report format.
- Affected databases: **none directly** (CLI/reporting only; all backends share it).
- **Out of scope**: choosing report formats, other CLI validation (T1), changes to report content.

## Capabilities

### New Capabilities
- `report-export`: where `--exportfile` writes NBomber reports and how the value is validated.

### Modified Capabilities
<!-- None. -->

## Impact

- Code: `src/NBomber.YCSB/Infra/YcsbCliArgsValidator.cs` (export file rule), `src/NBomber.YCSB/YcsbScenario.cs` (folder + file name). Tests: offline validator tests (e.g. next to the existing validator tests in `ScanLengthTests.cs`, or a new `ExportFileTests.cs`).
- **Benchmark results: no change** (measurements, workloads and comparability are unaffected). Only where report files are written changes, and `--exportfile` starts working.
