# Design

## Context

`YcsbScenario.WithInit` runs `InitDb` → `DeleteAllData` → `BulkInsert(tableName = "test_table")`. `IDbYcsbClient.DeleteAllData()` has no parameters. Postgres and Scylla use their own table `ycsb_table`; MongoDB uses database `ycsb`; RavenDB `InitDb` hard-deletes and recreates its database; Redis `InitDb` does nothing and `DeleteAllData` is `FLUSHDB`.

User decision (2026-11-18): `DeleteAllData` only deletes, `InitDb` only creates, no `RecreateTable()`, and the scenario orchestrates: **reset first**.

## Goals / Non-Goals

- Goals: one responsibility per method; scenario order `DeleteAllData → InitDb → BulkInsert`; every reset succeeds on a fresh database; MongoDB reset limited to `ycsb`; no interface change.
- Non-goals: Redis/RavenDB delete scope; changing measured operations or loaded data.

## Decisions

### YcsbScenario
Swap the two calls in `WithInit`: `await ycsbClient.DeleteAllData(); await ycsbClient.InitDb();` then generate and bulk-load as today.

### ScyllaDB
- `DeleteAllData`: `DROP TABLE IF EXISTS {keyspace}.ycsb_table` only. Verified on Scylla 2026.1.5: `IF EXISTS` succeeds when the keyspace does not exist, so no keyspace check is needed (user decision 2026-11-18; avoids the driver schema cache).
- `InitDb`: create keyspace if not exists; `CREATE TABLE IF NOT EXISTS` with columns from `fieldcount`; clear the three statement caches; prepare insert/read/scan; warm up single-field statements (the current InitDb body minus the DROP).
- `RecreateTable()` is removed.
- Stale-column risk: `CREATE TABLE IF NOT EXISTS` keeps an existing column set when `InitDb` runs without a reset; the scenario always resets first. Note this in an InitDb comment (moved from the old RecreateTable comment) and update the `?? Prepare*()` fallback comment: after DeleteAllData, InitDb must run before any operation.

### PostgreSQL
- `DeleteAllData`: `DROP TABLE IF EXISTS ycsb_table` (no `CASCADE`); logging and rethrow as today.
- `InitDb`: `CREATE TABLE IF NOT EXISTS ycsb_table (shared column definition)`; no `DROP INDEX` (user decision A).
- Auto-prepared statements: plans are re-analysed by table name after DDL; result types unchanged.

### MongoDB
`DeleteAllData` = `DropDatabaseAsync(databaseName)`; `InitDb` unchanged (no-op).

### RavenDB (forced by the order change) — R2 chosen
`DeleteAllData` = the existing `DeleteDatabasesOperation(hardDelete: true)` with the `catch DatabaseDoesNotExistException`, moved out of `InitDb`; `InitDb` = `CreateDatabaseOperation` + warm-up. R1 (catch in the current DeleteByQuery reset) rejected: with reset first it would run `DeleteByQuery("from @all_docs")` on the previous run's full database and can time out (60 s) on large record counts, only for InitDb to delete the database right after. Update the Scan comment that says InitDb recreates the database.

### Redis
Unchanged; order-independent.

### Tests
- Postgres: `DeleteAllData_Removes_All_Rows_And_Keeps_Table_Usable`, `DeleteAllData_Removes_Leftover_Index_And_Leaves_Only_Primary_Key`, `DeleteAllData_Then_BulkInsert_Stores_Fresh_Values`, `DeleteAllData_Succeeds_On_Empty_Table` call `InitDb()` right after `DeleteAllData()` (the second reset in the empty-table test covers "table does not exist").
- Scylla: `DeleteAllData_Keeps_Other_Tables_In_Keyspace` calls `InitDb()` in `finally` (other tests need the table); `DeleteAllData_Recreates_Empty_Table_With_Configured_Columns` calls `InitDb()` after the reset and is renamed `DeleteAllData_Then_InitDb_Gives_Empty_Table_With_Configured_Columns`; new fresh-keyspace test with `scylladb.keyspace=ycsb_fresh_<guid>` (reset succeeds and creates nothing; InitDb + insert/read work; keyspace dropped in `finally`); update the class comment about InitDb/DeleteAllData.
- RavenDB: new fresh-database test with `ravendb.database=ycsb_fresh_<guid>` (reset succeeds, InitDb works, database deleted in `finally`).
- `IDbYcsbClient` doc comments describe the new contract (comments only).

### Destructive operations
Reset is destructive by design (benchmark data only). MongoDB scope narrowed to `ycsb`.

## Risks / Trade-offs

- [Order change affects all five clients] → each client checked above; Redis/Mongo order-independent; Raven needs the fresh-server fix.
- [Postgres DROP needs table ownership] → the benchmark user creates and owns `ycsb_table`. Lock: ACCESS EXCLUSIVE, same as TRUNCATE.
- [Scylla `auto_snapshot`] → each DROP leaves a snapshot of the dropped table; negligible for benchmark data.

## Migration Plan

None. Rollback is reverting the change.
