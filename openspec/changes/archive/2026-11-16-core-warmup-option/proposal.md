# Proposal

## Why

`YcsbScenario` hard-codes `.WithWarmUpDuration(TimeSpan.FromSeconds(3))` (`YcsbScenario.cs`, before `WithLoadSimulations`). Original YCSB has no warm-up: each client thread runs exactly its share of `operationcount` and every operation is measured (`ClientThread.java` run loop; no "warm" anywhere in YCSB-master).

An offline spike with a call-counting fake `IDbYcsbClient` against the current code (NBomber 6.4.1, `recordcount` 100, `operationcount` 1000) showed:

| Case | Fake DB operation calls | Measured (NodeStats steps) | Warm-up extra |
|---|---|---|---|
| Workload A, 1 thread, no delay | 2000 | 1000 | 1000 |
| Workload D, 1 thread, no delay | 2000 (101 inserts) | 1000 (43 inserts) | 1000 (58 inserts) |
| Workload A, 4 threads, ~5 ms per op | 1672 | 1000 | 672 (time-capped) |

So, confirmed:
- Warm-up operations are **additional** to `operationcount`; the measured phase still runs the full `operationcount`.
- Warm-up uses the same load simulation and operation mix, bounded by the iteration count for a fast database and by the 3 s duration for a slow one.
- Warm-up operations **reach the database and change its state** (updates, inserts); warm-up inserts also advance the shared insert key counter and acknowledged key limit, so workloads D and E start measuring on a larger dataset.
- Warm-up operations are **excluded** from the returned `NodeStats`.
- `InitDb`, `DeleteAllData` and `BulkInsert` run once.

Consequences: results are not comparable with Java YCSB (hidden, unreported operations), and not comparable across databases (a fast database runs many more warm-up operations in 3 s than a slow one, so each starts measurement from a different cache state and, for D/E, a different dataset size).

A database-client fix is not possible: warm-up is configured on the NBomber scenario in the shared Core and affects every database the same way.

## What Changes

- New typed option `--warmup <seconds>` (whole seconds, default **0**), like `--minscanlength`.
- Warm-up 0: the scenario runs **without warm-up** (`WithoutWarmUp()`, because NBomber otherwise applies its own default warm-up), so the database receives exactly `operationcount` operations after the initial load, as in YCSB.
- Warm-up > 0: `WithWarmUpDuration` of that many seconds; `--warmup 3` reproduces the previous behaviour exactly.
- Validation rejects a negative warm-up.
- Documentation: results produced before this change used a fixed 3-second warm-up (extra, unreported operations that also changed data) and may not be directly comparable with new results; `--warmup 3` restores the old behaviour.
- No new tests (user decision): the warm-up behaviour was measured by the pre-plan fake-client spike above; the change is verified by build, the existing offline tests and code review.
- Affected databases: **all**, through the shared Core. No database client changes; client-level `InitDb` warm-ups (ScyllaDB prepared statements, RavenDB connection pool) are unchanged.
- **Out of scope**:
  - Database-specific `InitDb` warm-up behaviour (user decision).
  - Warm-up by operation count (no NBomber C# builder for warm-up simulations).
  - Validation of other options (T1), exit codes.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `core-workload`: adds requirements for the measured operation count, the opt-in warm-up option and its validation.

## Impact

- Code: `src/NBomber.YCSB/Infra/YcsbCliArgs.cs` (option), `src/NBomber.YCSB/Infra/YcsbCliArgsValidator.cs` (rule), `src/NBomber.YCSB/YcsbScenario.cs` (warm-up selection). Docs: `README.md`, `CLAUDE.md`. No new test file.
- **Behaviour change (default):** runs no longer include a hidden 3-second warm-up. For a fast database this roughly halves the operations the database receives for the same `operationcount`; measured latency may rise slightly because .NET JIT and connection setup now fall inside the first measured operations. Results from before this change are not directly comparable.
- Live `Workload_*` tests build `YcsbCliArgs` directly, get warm-up 0, keep passing (they check only the operation mix) and run about 3 s faster each.
