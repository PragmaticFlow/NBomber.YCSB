# Design

## Context

`YcsbCliArgsValidator` requires an existing directory for `--exportfile`; `YcsbScenario.Run` calls `runner.WithReportFileName(settings.ExportFile)`. NBomber 6.4.1 rejects path separators in the report file name and has `WithReportFolder(folder)` for the location (probe results in proposal.md).

## Goals / Non-Goals

- Goals: `--exportfile` works for bare names, relative and absolute paths; validation matches what NBomber accepts; minimal code.
- Non-goals: report format selection; extension handling; other CLI validation (T1).

## Decisions

### YcsbScenario
```csharp
if (!string.IsNullOrWhiteSpace(settings.ExportFile))
{
    var folder = Path.GetDirectoryName(settings.ExportFile);
    if (!string.IsNullOrEmpty(folder))
        runner = runner.WithReportFolder(folder);

    runner = runner.WithReportFileName(Path.GetFileName(settings.ExportFile));
}
```
`IsNullOrWhiteSpace` matches the validator's `When` condition (today `!= null` would pass an empty string to NBomber).

### YcsbCliArgsValidator
Replace `DirectoryExists` with one `.Must(IsValidExportFile)` private static method, still under `When(!IsNullOrWhiteSpace(ExportFile))` (a whitespace-only value is treated as not set):
- `Path.GetFileName(value)` is not null/whitespace;
- the file name contains no `Path.GetInvalidFileNameChars()`;
- the folder part (`Path.GetDirectoryName`), if any, contains no `Path.GetInvalidPathChars()`.
Message (existing lambda style): `.WithMessage(x => $"exportfile ({x.ExportFile}) must be a valid path ending with a file name.")`. The `try/catch` goes away (`Path.GetFileName`/`GetDirectoryName` do not throw on .NET Core for bad characters).

### Alternatives considered
- Accept only bare file names: simplest, but YCSB's `exportfile` is a path and users expect folders to work.
- Keep "folder must exist": stricter than NBomber, which creates the folder; YCSB would fail on a missing folder, but failing a long run for a missing folder gives no benefit here.

### Destructive operations
None. The validator runs in `Program.cs` before the client is built and before `YcsbScenario.Run`, so a rejected value never reaches `InitDb`/`DeleteAllData`. Probe: NBomber also rejects a bad report file name or folder before `WithInit` runs, so even a value that slipped past our validator could not reach the database.

### Hot path
Not affected (runs once at startup).

## Risks / Trade-offs

- [Reports for a path value are written without the timestamp subfolder] → Repeated runs with the same `--exportfile` overwrite each other's files. That is what a file path means; bare names keep the timestamped location.
- [Invalid file-name characters differ per OS] → NBomber 6.4.1 references `GetInvalidFileNameChars`/`GetInvalidPathChars` (inferred from assembly metadata; on Windows the probe confirmed it rejects `:`, `*`, `?`, `|`). The validator uses the same per-OS sets, so `:` and `|` are valid on Linux and invalid on Windows.
- [Folder creation or permission errors] → Appear only when reports are written, after the run (same as YCSB, which exports at the end). No existence check is reintroduced.
- [Odd but valid names] → `C:name` (drive-relative folder), trailing dots/spaces (`results..csv`), `.`/`..` as names are accepted; NBomber handles them.

## Migration Plan

None. Previously `--exportfile` could not work, so no existing usage changes.
