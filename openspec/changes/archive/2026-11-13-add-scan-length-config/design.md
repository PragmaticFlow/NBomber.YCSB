# Design

## Context

See proposal.md - Why. Today the scan step draws `context.Random.Next(1, 10)` inline (`YcsbScenario.cs:69`). Settings come from typed `System.CommandLine` options in `YcsbCliArgs` (each with a `DefaultValueFactory`), are validated by `YcsbCliArgsValidator` (FluentValidation, one rule today), and are read by `DataGenerator` through its primary constructor. Workload-specific values live in `WorkloadManager` switches (`GetOperations`, `GetDescription`). The live workload tests build `YcsbCliArgs` directly (`RedisTests.cs:219-231`) without going through `FromParseResult`, so any default must also apply to directly constructed settings. Requirements are in `specs/core-workload/spec.md`.

## Goals / Non-Goals

**Goals:**
- Two typed options with YCSB names, workload-aware maximum default, one validation rule set, one place that draws the length.
- No new classes, interfaces or packages; `IDbYcsbClient` unchanged.

**Non-Goals:**
- `scanlengthdistribution`, zipfian lengths, `-p` property aliases, database-specific scan fixes.

## Decisions

### Settings shape in `YcsbCliArgs`
- `public int MinScanLength { get; set; } = 1;` with `Option<int>("--minscanlength")`, `DefaultValueFactory = _ => 1`. The minimum default is 1 for every workload, so it needs no nullability.
- `public int? MaxScanLength { get; set; }` with `Option<int?>("--maxscanlength")` and no default factory. `null` means "use the workload default".
- `public int GetMaxScanLength() => MaxScanLength ?? WorkloadManager.GetDefaultMaxScanLength(Workload);` is the single resolution point, used by the validator and `DataGenerator`.
- Option descriptions: `--minscanlength` "Minimum number of records per scan (default: 1)"; `--maxscanlength` "Maximum number of records per scan (default: 100 for workload E, 1000 otherwise)".
- **Alternative: resolve the default inside `FromParseResult`**: rejected, because directly constructed settings (the workload tests) would keep `null` and need the same fallback anyway.
- **Alternative: a workload-dependent `DefaultValueFactory`**: rejected; the factory cannot reliably read the `--workload` value, and it would not cover direct construction either.

### Workload default in `WorkloadManager`
Add `GetDefaultMaxScanLength(Workload workload)` returning 100 for `Workload.E` and 1000 otherwise, as a `switch` in the style of `GetDescription`. This keeps every per-workload preset in one class and mirrors `workloade:42` versus `CoreWorkload.java:294`.

### Validation in `YcsbCliArgsValidator`
- `RuleFor(x => x.MinScanLength).GreaterThanOrEqualTo(1).WithMessage("minscanlength must be at least 1.")`
- `RuleFor(x => x.MinScanLength).LessThanOrEqualTo(x => x.GetMaxScanLength()).WithMessage(x => $"minscanlength ({x.MinScanLength}) must not exceed maxscanlength ({x.GetMaxScanLength()}).")`
- A maximum below 1 is covered by the second rule, because min is at least 1.

### Drawing the length in `DataGenerator`
- Capture `_minScanLength = settings.MinScanLength` and `_maxScanLength = settings.GetMaxScanLength()` like the existing fields.
- `public int NextScanLength(Random random) => (int)random.NextInt64(_minScanLength, (long)_maxScanLength + 1);`
- `NextInt64` with a `long` upper bound avoids overflow when `maxscanlength` is `int.MaxValue`; the result always fits in `int`.
- The scan step calls `dataGen.NextScanLength(context.Random)`. Using NBomber's per-scenario-copy `Random` keeps the current thread-safety model and makes the method testable with a seeded `Random`.
- **Alternative: keep the draw inline in `YcsbScenario`**: rejected, because it cannot be unit tested without NBomber.

### Hot-path cost
One `NextInt64` call per scan, replacing one `Next` call. No allocations, round trips or prepared-statement changes. `IDbYcsbClient.Scan(table, startkey, recordcount, fields)` is unchanged.

### Destructive operations
None. No schema or data changes; offline tests only.

## Risks / Trade-offs

- [Workload E results change (about 10x more rows per scan)] → Intended YCSB behaviour; note in the change summary that earlier E results are not comparable.
- [MongoDB still returns at most one record per scan, RavenDB excludes the start key, ScyllaDB scans in token order] → Out of scope; cross-database E comparisons remain unreliable until those database changes land.
- [Very large `maxscanlength` makes individual scans expensive] → User-controlled; same as YCSB. No upper cap is added.
- [A validation failure still exits with code 0, because `Program.cs` returns without setting an exit code] → Pre-existing behaviour, out of scope.

## Migration Plan

No migration. Existing command lines keep working; workload E picks up the new 1..100 range automatically. Rollback is reverting the change.
