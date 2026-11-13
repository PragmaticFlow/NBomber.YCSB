# Proposal

## Why

Every Scan operation asks for `Random.Next(1, 10)` records (`YcsbScenario.cs:69`), so scan lengths are always 1..9 (mean 5). Original YCSB draws each scan length uniformly from `minscanlength..maxscanlength` inclusive (`CoreWorkload.java:535-536`, `UniformLongGenerator.java:44`), with defaults 1 and 1000 (`CoreWorkload.java:284,294`) and workload E overriding the maximum to 100 (`workloads/workloade:42`). Workload E is 95% scans, so each of our scans does roughly a tenth of the work of a YCSB scan, inflating throughput and making workload E results incomparable with YCSB and misleading across databases. The length is also not configurable.

## What Changes

- Add two typed options to `YcsbCliArgs`: `--minscanlength` (default 1) and `--maxscanlength` (default depends on the workload: 100 for E, 1000 for all others), following YCSB's property names. They are typed options, not `-p` props, because they are shared Core workload settings like `--fieldcount`; `-p` props stay reserved for database settings. A YCSB-style `-p maxscanlength=...` is passed to the database client and ignored for scan length.
- An explicitly supplied value overrides the workload default.
- Each scan's record count is drawn uniformly from `minscanlength..maxscanlength`, both bounds inclusive. The distribution is fixed to uniform, YCSB's default.
- Validation rejects a configuration unless `1 <= minscanlength <= maxscanlength`, using the effective maximum (explicit or workload default).
- Offline unit tests for defaults, overrides, validation and the scan length range.
- Affected databases: **all**, through the shared Core. No database client code changes; `IDbYcsbClient.Scan` already receives the record count.
- **Out of scope**:
  - `scanlengthdistribution` and zipfian scan lengths (fixed to uniform for now).
  - Java-style `-p minscanlength=` / `-p maxscanlength=` property syntax (typed options only, like the existing `--fieldcount`).
  - Database-specific scan behaviour: MongoDB scans with an equality filter (returns at most one record), RavenDB excludes the start key, ScyllaDB scans in token order. Each will be its own change.
  - Other Core roadmap items (key distribution, insert acknowledgement, operation overrides).

## Capabilities

### New Capabilities
- `core-workload`: the shared YCSB Core workload behaviour that applies to every database, starting with scan length configuration and generation.

### Modified Capabilities
<!-- None. The only existing capability, postgres-client, is unaffected. -->

## Impact

- Code: `src/NBomber.YCSB/Infra/YcsbCliArgs.cs`, `Infra/YcsbCliArgsValidator.cs`, `Infra/Workload.cs` (`WorkloadManager`), `Infra/DataGenerator.cs`, `YcsbScenario.cs` (scan step). Tests: new offline test file in `tests/NBomber.YCSB.Tests`.
- No interface, dependency, compose or CI changes. Existing command lines keep working; omitted options take the workload defaults.
- Benchmark results: workload E scans become about 10x larger on average (mean 50.5 vs 5), so E throughput drops and latency rises for every database. Results before this change are not comparable with results after it, but after it they are comparable with original YCSB. Scans in other workloads only occur when proportions are overridden, which is not yet supported, so A-D and F are unaffected in practice.
- The live `Workload_E` tests check only the operation mix (95% scan, 5% insert), so they keep passing, but they run slower because each scan returns more rows.
- Cross-database comparability for E still depends on the out-of-scope database scan fixes: until MongoDB's scan is fixed it returns at most one record regardless of the requested length.
