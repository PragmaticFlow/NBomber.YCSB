# Design

## Context

See proposal.md - Why. `DeleteAllData` runs once per scenario during NBomber's init phase (`InitDb` → `DeleteAllData` → `BulkInsert`), never inside a timed operation, so it has no effect on measured latency or throughput. `InitDb` runs immediately before it and creates `ycsb_table` (with its primary key and GIN index) when the table is missing, so the table always exists when `DeleteAllData` is called from the scenario. Requirements are in `specs/postgres-client/spec.md`.

## Goals / Non-Goals

**Goals:**
- A single valid statement that empties `ycsb_table` and keeps its structure and indexes.
- No change to the `IDbYcsbClient` contract or to how the result is reported.

**Non-Goals:**
- Changing how `YcsbScenario` reacts to a failed cleanup (shared by all databases).
- Changing `InitDb`, `BulkInsert`, or the table schema.

## Decisions

### Use `TRUNCATE TABLE ycsb_table`
Replace `TRUNCATE TABLE IF EXISTS ycsb_table` with `TRUNCATE TABLE ycsb_table`, built from the existing `TABLE_NAME` constant.

- **Why TRUNCATE**: it empties the table in one fast, constant-time operation regardless of row count, reclaims storage immediately, and keeps columns, the primary key and the GIN index. It matches the ScyllaDB client, which also uses `TRUNCATE` and relies on `InitDb` having created the table. It also resets the primary key and GIN index to empty. `DELETE` would leave dead tuples, index bloat and GIN pending-list entries, so the following bulk load and reads would depend on autovacuum state, harming reproducibility and cross-run comparability.
- **Alternative: `DELETE FROM ycsb_table`**: also valid, but it scans every row, writes WAL per row, leaves dead tuples for VACUUM, and gets slower as `recordcount` grows. Rejected.
- **Alternative: keep "if exists" semantics** with a `DO` block or a `to_regclass` check: unnecessary because `InitDb` always creates the table first, and a missing table is a genuine error that should fail the cleanup. Rejected.
- **Alternative: `DROP TABLE` + re-create**: would duplicate `InitDb`'s schema logic and is a schema-changing operation. Rejected.

### Log and rethrow
Keep the `try/catch` and its stderr log line, but replace `return Response.Fail(statusCode: "data clean failed")` with `throw;`. This matches `InitDb` and `BulkInsert` in the same client (both log and rethrow) and the other four clients, whose `DeleteAllData` does not catch at all. A failed cleanup then aborts NBomber's init instead of letting the run continue on stale data. `YcsbScenario` needs no change.

- **Alternative: keep returning `Fail`**: the scenario ignores the result, so a genuine cleanup failure (privilege, lock timeout) would silently bring back the stale-data problem. Rejected (user decision, option B).

### Destructive operation
`TRUNCATE` permanently removes all rows of `ycsb_table`. That is the intended behaviour, and the old code already intended it on every run; the difference is that it now succeeds. It touches only `ycsb_table`, takes an `ACCESS EXCLUSIVE` lock for its short duration, and is not schema-changing. It is not cascaded (no `CASCADE`), so it fails rather than truncating other tables if a foreign key ever references `ycsb_table`. TRUNCATE must acquire its lock and waits for any open transaction that has touched `ycsb_table` (for example an idle-in-transaction session or a concurrent run on the same database); it then fails at the Npgsql command timeout (30 s default). It requires the TRUNCATE privilege; the table owner in the compose setup has it.

### Hot-path cost
None. The statement runs once per scenario in init, outside measured operations. No new allocations, round trips or prepared statements in the request path.

## Risks / Trade-offs

- [Every Postgres run now really empties `ycsb_table`] → This is the intended YCSB behaviour and matches the other databases. Point the tool only at a disposable benchmark database, as for every other backend.
- [Results change compared with earlier Postgres runs on a persistent volume] → Expected: earlier runs started from leftover data. Note it in the change summary.
- [`ACCESS EXCLUSIVE` lock blocks concurrent access to `ycsb_table` during the truncate] → Only happens in init, before load starts. No other process should be using the benchmark table.
- [Another session holds a transaction on `ycsb_table`] → TRUNCATE waits, then fails at the command timeout; the error is logged and rethrown, aborting init.
- [Database user lacks the TRUNCATE privilege (external databases)] → Cleanup fails loudly and aborts the run; the compose user owns the table.
- [Known, out of scope: `InitDb` checks for the table only in the `public` schema, while all other statements use the unqualified name via `search_path`] → No change in this fix.

## Migration Plan

No migration. The next run on any existing database empties `ycsb_table` before loading. Rollback is reverting the two-line change.
