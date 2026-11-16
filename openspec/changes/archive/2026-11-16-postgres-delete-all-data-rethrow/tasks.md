# Tasks

## 1. Rethrow on failed cleanup (Postgres client)

- [x] 1.1 In `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLYcsbClient.cs`, in `DeleteAllData`, replace `return Response.Fail(statusCode: "data clean failed");` with `throw;`; keep the `try`/`catch` and the `Console.Error.WriteLine` line; no other changes

## 2. Test

- [x] 2.1 In `tests/NBomber.YCSB.Tests/PostgresNoSqlTests.cs`, add `DeleteAllData_Throws_When_Database_Is_Unreachable` as in design.md (new client with `postgres.host=127.0.0.1`, `postgres.port=1` and the file's other default props; call only its `DeleteAllData()`, not `CreateClient()` or `InitDb()`; `await Assert.ThrowsAnyAsync<NpgsqlException>(() => client.DeleteAllData())`), following the file's existing style
- [x] 2.2 Run only that test with an exact filter (`dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName=NBomber.YCSB.Tests.PostgresNoSqlTests.DeleteAllData_Throws_When_Database_Is_Unreachable"`); it connects to an unused port and touches no database; verify it passes

- [x] 2.3 Review follow-up (user request): in `DeleteAllData_Throws_When_Database_Is_Unreachable`, build props with `var props = CreateProps();`, set `props["postgres.host"] = "127.0.0.1";` and `props["postgres.port"] = "1";`, and pass `props` to the client, removing the duplicated literal props; re-run only that test with the exact filter from 2.2 and verify it passes

## 3. Build and regression

- [x] 3.1 Run `dotnet build NBomber.YCSB.sln`; verify 0 errors and no new warnings in touched files
- [x] 3.2 Run the offline tests (`dotnet test tests/NBomber.YCSB.Tests --no-build --filter "FullyQualifiedName~DataGeneratorTests|FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"`); verify all pass

## Workflow follow-up

- Architect review and user approval before implementation (backend-task Stage 2).
- Optional, requires explicit approval: run the three existing `DeleteAllData_*` live tests against the project's own local Postgres container (after checking port 5432 is that container) to confirm the success path is unchanged.
- Archive only after explicit user approval (no spec sync: `skip_specs: true`).
