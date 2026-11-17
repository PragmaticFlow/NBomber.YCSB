# report-export Specification

## Purpose

Defines where the `--exportfile` option writes the benchmark reports produced by NBomber and which values the option accepts.

## Requirements

### Requirement: Export file location
When `--exportfile` is set, the reports SHALL be written with the value's file name. If the value contains a folder, the reports SHALL be written directly into that folder (relative folders resolved against the current working directory), which SHALL be created if it does not exist. If the value is a bare file name, the reports SHALL be written to NBomber's default report location. When `--exportfile` is not set or is whitespace only, report output SHALL be unchanged.

#### Scenario: Bare file name
- **WHEN** the run uses `--exportfile results`
- **THEN** the run is accepted and the reports are written as `results.<format>` in `reports/<timestamp>/`

#### Scenario: Relative path
- **WHEN** the run uses `--exportfile out/results` and the folder `out` does not exist
- **THEN** the run is accepted, `out` is created and the reports are written as `out/results.<format>`

#### Scenario: Absolute path
- **WHEN** the run uses an absolute path such as `/tmp/bench/redis-a` or `C:/bench/redis-a`
- **THEN** the reports are written as `<that path>.<format>`

#### Scenario: Name with an extension
- **WHEN** the run uses `--exportfile results.json`
- **THEN** the file name is used as given and NBomber appends its format extension, e.g. `results.json.html`

### Requirement: Export file validation
A `--exportfile` value SHALL be rejected before the run starts, with a message naming exportfile, if it has no file name (for example it ends with a path separator, or the file name is whitespace), if its file name contains characters that are invalid in a file name, or if its folder contains characters that are invalid in a path. A folder that does not exist SHALL NOT make the value invalid.

#### Scenario: Folder only
- **WHEN** the run uses `--exportfile out/`
- **THEN** validation fails with a message naming exportfile and no database operation runs

#### Scenario: Invalid file name characters
- **WHEN** the file name part contains a character from `Path.GetInvalidFileNameChars()`
- **THEN** validation fails with a message naming exportfile

#### Scenario: Whitespace file name
- **WHEN** the file name part is whitespace, e.g. `out/ `
- **THEN** validation fails with a message naming exportfile

#### Scenario: Invalid folder characters
- **WHEN** the folder part contains a character from `Path.GetInvalidPathChars()`
- **THEN** validation fails with a message naming exportfile

#### Scenario: Missing folder is allowed
- **WHEN** the run uses `--exportfile not-yet-created/results`
- **THEN** validation passes
