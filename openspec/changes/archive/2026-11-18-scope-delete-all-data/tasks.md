# Tasks

Revised 2026-11-18 (user): reset first, separate DeleteAllData (delete) and InitDb (create), no RecreateTable. The earlier implementation in the working tree is reworked by these tasks.

## 0. Decisions

- [x] 0.1 No `DROP INDEX` in Postgres `InitDb` (user decision A, 2026-11-18)
- [x] 0.2 Update this change instead of a new one; scenario order DeleteAllData → InitDb → BulkInsert (user, 2026-11-18)
- [x] 0.3 Fix `openspec/changes/fix-postgres-client/specs/postgres-client/spec.md` wording (reset drops the table, initialisation recreates it) — done by the Team Lead

## 1. Core and clients

- [x] 1.1 `src/NBomber.YCSB/YcsbScenario.cs`: call `DeleteAllData()` before `InitDb()` in `WithInit`
- [x] 1.2 `src/NBomber.YCSB/ScyllaDb/ScyllaYcsbClient.cs`: remove `RecreateTable()`; `DeleteAllData` = keyspace metadata guard + `DROP TABLE IF EXISTS`; `InitDb` = keyspace if not exists + `CREATE TABLE IF NOT EXISTS` + clear caches + prepare + warm-up; InitDb comment about keeping an existing table; update the `?? Prepare*()` fallback comment
- [x] 1.3 `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLYcsbClient.cs`: `DeleteAllData` = `DROP TABLE IF EXISTS` only (no `CASCADE`, log and rethrow); `InitDb` = `CREATE TABLE IF NOT EXISTS` (shared definition, no index, no `DROP INDEX`)
- [x] 1.4 `src/NBomber.YCSB/MongoDb/MongoDbYcsbClient.cs`: `DeleteAllData` = `DropDatabaseAsync(databaseName)` (done in the first version; keep)
- [x] 1.5 `src/NBomber.YCSB/RavenDb/RavenDbYcsbClient.cs` (R2): move the hard-delete (with the does-not-exist catch) to `DeleteAllData`; `InitDb` = create database + warm-up; update the Scan comment
- [x] 1.6 `src/NBomber.YCSB/Infra/IDbYcsbClient.cs`: doc comments only — DeleteAllData runs first during scenario init, removes the benchmark data (may drop benchmark structures), must succeed on a never-used database; InitDb runs after DeleteAllData and creates what the run needs
- [x] 1.7 Comment cleanup (user, 2026-11-18): in the .cs files changed on this branch (vs dev), remove unnecessary comments that only restate what the code obviously does; keep comments that explain a non-obvious reason (YCSB/Java parity, driver or database quirks, safety, ordering). Keep XML doc comments of public interfaces (IDbYcsbClient). No code changes besides comments.

## 2. Tests (live, **require explicit approval to run**)

- [x] 2.1 `PostgresNoSqlTests.cs`: `DeleteAllData_Removes_All_Rows_And_Keeps_Table_Usable`, `DeleteAllData_Removes_Leftover_Index_And_Leaves_Only_Primary_Key`, `DeleteAllData_Then_BulkInsert_Stores_Fresh_Values`, `DeleteAllData_Succeeds_On_Empty_Table` call `InitDb()` right after `DeleteAllData()`; other-table test kept
- [x] 2.2 `ScyllaDbTests.cs`: `DeleteAllData_Keeps_Other_Tables_In_Keyspace` calls `InitDb()` in `finally`; rename `DeleteAllData_Recreates_Empty_Table_With_Configured_Columns` → `DeleteAllData_Then_InitDb_Gives_Empty_Table_With_Configured_Columns` and call `InitDb()` after the reset; new fresh-keyspace test (`scylladb.keyspace=ycsb_fresh_<guid>`: reset succeeds and creates nothing; InitDb + insert/read work; keyspace dropped in `finally`); update the class comment
- [x] 2.3 `MongoDbTests.cs`: reset keeps another database (done in the first version; keep)
- [x] 2.4 `RavenDbTests.cs`: new fresh-database test (`ravendb.database=ycsb_fresh_<guid>`: reset succeeds, InitDb works, database deleted in `finally`); existing workload tests cover the new order

## 3. Docs, build, regression

- [x] 3.1 `CLAUDE.md` (Team Lead): live-test line "call `DeleteAllData`, then `InitDb`"; cleanup list (Scylla drops `ycsb_table`, keyspace kept, InitDb recreates it; Postgres drops `ycsb_table`, other tables untouched, InitDb recreates it); YcsbScenario bullet "Init runs `DeleteAllData`, then `InitDb`, then `BulkInsert`"; Gotchas: legacy index disappears because `DeleteAllData` drops `ycsb_table`
- [x] 3.2 `dotnet build NBomber.YCSB.sln`: 0 errors; offline tests pass
- [x] 3.3 Live tests for MongoDB, PostgreSQL, ScyllaDB, RavenDB, Redis (**requires explicit approval to run**; Postgres only after port 5432 is served by this project's container)
