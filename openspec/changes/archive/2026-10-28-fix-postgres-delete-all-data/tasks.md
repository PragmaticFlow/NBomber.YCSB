# Tasks

## 1. Fix Postgres DeleteAllData

- [x] 1.1 In `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLYcsbClient.cs`, change the `DeleteAllData` SQL from `TRUNCATE TABLE IF EXISTS {TABLE_NAME}` to `TRUNCATE TABLE {TABLE_NAME}`, and in its catch block replace `return Response.Fail(statusCode: "data clean failed");` with `throw;`, keeping the stderr log line; verify by inspecting the diff (only these two lines change, CRLF preserved)
- [x] 1.2 Build with `dotnet build NBomber.YCSB.sln` (use `-p:NuGetAudit=false` on the command line only if the pre-existing MessagePack NU1902 audit error blocks the build); verify 0 errors and no new warnings in `PostgresNoSQLYcsbClient.cs`
- [x] 1.3 Run the existing offline tests (`dotnet test tests/NBomber.YCSB.Tests --no-build --filter "FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"`); verify all pass. No offline test is possible for this change because the fix needs a live PostgreSQL to exercise

## 2. Live-database tests (require explicit approval to run)

- [x] 2.1 In `tests/NBomber.YCSB.Tests/PostgresNoSqlTests.cs`, add `DeleteAllData_Removes_All_Rows_And_Keeps_Table_Usable`: call `InitDb`, insert two rows with unique `test_` keys, call `DeleteAllData`; assert the response is not an error, `SELECT count(*) FROM ycsb_table` returns 0, and (read-only) `pg_indexes` still lists the primary key index and `idx_ycsb_table_jsonb` for `ycsb_table`; then `Insert` and `Read` a new unique key without calling `InitDb` and assert both succeed. Delete all three test keys in `finally` with the existing `DeleteRow` helper. Verify it compiles in task 1.2's build (old code: `DeleteAllData` returns `Fail` and the count stays 2)
- [x] 2.2 Add `DeleteAllData_Then_BulkInsert_Stores_Fresh_Values`: insert a row for a unique key with old values, call `DeleteAllData`, `BulkInsert` the same key with a different field set and values, and assert the stored JSON contains exactly the new field set and values; delete the test key in `finally` with `DeleteRow`. Verify it compiles (old code keeps the old values because of `ON CONFLICT DO NOTHING`)
- [x] 2.3 Verify by diff review (no test) that the statement names only `ycsb_table`, has no `CASCADE`, and creates or drops nothing. This covers the spec scenarios "Other data is untouched" and "Failed reset". There is no automated failure-path test: `_dataSource` is static and shared, so a client with a bad connection would interfere with `PostgresNoSqlTests`, and dropping `ycsb_table` is not allowed
- [x] 2.4 Reuse the existing helpers in `PostgresNoSqlTests.cs` (same compose settings: localhost:5432, mydb, myuser), add no packages or csproj changes, and list both tests as "added but not run" in the implementation report; verify by reviewing the diff
- [x] 2.6 Add `DeleteAllData_Succeeds_On_Empty_Table` (covers spec scenario "Reset on an empty table"): call `InitDb`, call `DeleteAllData` once to empty the table, call it again, assert the second response is not an error and `SELECT count(*) FROM ycsb_table` returns 0; verify it compiles
- [x] 2.7 In `DeleteAllData_Removes_All_Rows_And_Keeps_Table_Usable`, scope the `pg_indexes` query to `schemaname = 'public' AND tablename = @t` (matching `InitDb`'s `public` check); verify it compiles and the assertions are unchanged
- [x] 2.5 (Requires explicit approval to run) After verifying that the target is the project's own local Postgres container, run only the three new `DeleteAllData_*` tests with exact `FullyQualifiedName=` filters; verify all three pass and record the final database state. Note: these tests (and the workload tests, once fixed) truncate the whole `ycsb_table` in the local `postgres_data` volume; do not use `docker compose down -v`. Running the whole `PostgresNoSqlTests` class is safe because xUnit runs tests within a class sequentially and every test seeds its own rows

## Workflow follow-up

- Architect review and user approval before implementation (backend-task Stage 2).
- Code review and OpenSpec verification after implementation.
- Archive only after explicit user approval.
