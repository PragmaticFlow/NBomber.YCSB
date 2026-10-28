# Proposal

## Why

`PostgresNoSQLYcsbClient.DeleteAllData` runs `TRUNCATE TABLE IF EXISTS ycsb_table`. PostgreSQL's `TRUNCATE` has no `IF EXISTS` clause, so the statement always fails with a syntax error. The client catches it, logs to stderr and returns `Fail("data clean failed")`, and `YcsbScenario`'s init ignores that result. As a result the benchmark table is never cleared between runs. Because `BulkInsert` uses `ON CONFLICT (ycsb_key) DO NOTHING`, rows left over from earlier runs (including documents modified by earlier updates) silently survive and are not re-seeded. Each Postgres run therefore starts from an undefined data set rather than a fresh load of `recordcount` rows, which makes Postgres results incomparable with other databases and with its own earlier runs. Two further effects: workload D/E inserts use deterministic keys after `recordcount` and `Insert` has no conflict handling, so a run following an earlier D/E run records duplicate-key failures on the insert step; and rows left by a run with a different `fieldcount`/`fieldlength` keep their old field set, skewing read/scan sizes and partial updates.

## What Changes

- Make Postgres `DeleteAllData` remove all rows from the benchmark table using valid PostgreSQL syntax, so each run starts from an empty table before the bulk load.
- Keep the operation scoped to the benchmark table `ycsb_table` only. No other tables, schemas or databases are touched, and the table structure and indexes are preserved.
- On failure, log the error to stderr and rethrow (as `InitDb` and `BulkInsert` in the same client already do, and as every other client's `DeleteAllData` does by not catching), so a failed cleanup aborts the run instead of silently benchmarking stale data. `Ok` is returned on success.
- Affected database: **PostgreSQL only**.
- **Out of scope**:
  - Changing `YcsbScenario` to inspect `DeleteAllData`'s result (shared by all databases). Not needed: Postgres is the only client that catches cleanup errors, and other clients already throw and abort init; rethrowing makes Postgres consistent with them.
  - Changing `BulkInsert`'s `ON CONFLICT DO NOTHING`, the GIN index, or `InitDb`.
  - Cleanup behaviour of other databases (Redis flush, MongoDB database drop, RavenDB database delete), and Update's missing-key handling.
  - Repairing data in existing local volumes beyond what a successful cleanup on the next run does.

## Capabilities

### New Capabilities
- `postgres-client`: behaviour of the PostgreSQL YCSB client, starting with resetting benchmark data between runs.

### Modified Capabilities
<!-- None: no main specs exist yet. -->

## Impact

- Code: `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLYcsbClient.cs` (`DeleteAllData`). Tests: `tests/NBomber.YCSB.Tests/PostgresNoSqlTests.cs`.
- No interface, CLI, configuration, dependency, compose or CI changes.
- Benchmark results: Postgres runs will start from exactly `recordcount` freshly loaded rows, as the other databases already do. Results produced before this fix (on a persistent volume) are not comparable with results after it. CI is less affected because it starts a fresh container per job, but data from one workload test currently carries into the next within a job; after the fix each workload test starts clean.
- Safety: the operation is destructive by design, but only for the benchmark's own table, and it already runs on every workload start. It must not be run outside an approved, disposable test database.
