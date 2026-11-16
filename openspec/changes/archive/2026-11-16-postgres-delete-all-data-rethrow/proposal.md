# Proposal

## Why

`PostgresNoSQLYcsbClient.DeleteAllData` catches every exception, writes it to stderr and returns `Response.Fail(statusCode: "data clean failed")` (`PostgresNoSQLYcsbClient.cs`, `DeleteAllData`). `YcsbScenario.WithInit` ignores the returned response, so NBomber treats initialisation as successful and the benchmark runs. `BulkInsert` uses `ON CONFLICT DO NOTHING`, so rows from the previous run, with old field values or extra fields, survive, and the run measures stale data.

This contradicts the archived requirement **"Reset reports its outcome"** in `openspec/specs/postgres-client/spec.md`: when records cannot be removed, delete-all-data SHALL write the error to the error output **and propagate the error**, so initialisation fails. The archived change `2026-10-28-fix-postgres-delete-all-data` designed exactly this ("replace `return Response.Fail(...)` with `throw;`"), but the current code on `dev` still returns `Fail`: the `TRUNCATE` part of that fix is present and the rethrow is not.

NBomber stops the load test when the scenario init throws (NBomber.xml, `Scenario.WithInit`), so rethrowing is enough; no Core change is needed. The other four clients already throw from `DeleteAllData` on failure.

This replaces the Core-level alternative (check every init response in `YcsbScenario`), which the user decided against: only this client returns `Fail` from an init step.

## What Changes

- In `PostgresNoSQLYcsbClient.DeleteAllData`, keep the `try`/`catch` and its stderr log line, and replace `return Response.Fail(statusCode: "data clean failed");` with `throw;`, matching `InitDb` and `BulkInsert` in the same client.
- Add a test in `PostgresNoSqlTests` that `DeleteAllData` throws when the database cannot be reached (connection refused on an unused local port). It needs no running database and deletes nothing. In a real run an unreachable database already stops at `InitDb`; the cases only `DeleteAllData` meets are, for example, a missing TRUNCATE privilege or a lock timeout. The refused connection is a cheap way to prove the method propagates errors.
- No spec change: the code is brought in line with the existing `postgres-client` requirement (`skip_specs: true`).
- Affected databases: **PostgreSQL only**.
- **Out of scope**:
  - A Core-level check of init responses in `YcsbScenario` (decided against).
  - Process exit code after a failed init (T1).
  - Other Postgres follow-ups (Update returning Ok for a missing row, concurrent bulk-insert chunks, static `NpgsqlDataSource`).

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
<!-- None: implementation brought in line with the existing postgres-client requirement "Reset reports its outcome". -->

## Impact

- Code: `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLYcsbClient.cs` (one line). Tests: `tests/NBomber.YCSB.Tests/PostgresNoSqlTests.cs` (one new test).
- Behaviour: a failed Postgres cleanup now stops the run before warm-up instead of continuing on stale data. Successful cleanups are unchanged; the existing `DeleteAllData_*` tests only check the success path.
- Benchmark results: no effect on valid runs.
