# Design

## Context

See proposal.md - Why. Current code:

```csharp
public async Task<Response<object>> DeleteAllData()
{
    try
    {
        string sql = $"TRUNCATE TABLE {TABLE_NAME}";
        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
        return Response.Ok();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error during delete all data: {ex.Message}");
        return Response.Fail(statusCode: "data clean failed");
    }
}
```

`InitDb` and `BulkInsert` in the same client log to stderr and `throw;`. The client keeps its `NpgsqlDataSource` in a **static** field that each new client instance replaces (CLAUDE.md, Gotchas). Every test in `PostgresNoSqlTests` creates its own client through `CreateClient()` (host `localhost`, port `5432`), and xUnit runs the tests of one class sequentially. The `postgres-client` spec requirement "Reset reports its outcome" already requires logging and propagating the error.

## Goals / Non-Goals

**Goals:**
- A failed Postgres cleanup throws, so NBomber stops the run before warm-up.
- Keep the stderr log line and the success path unchanged.
- A test of the failure path that needs no database.

**Non-Goals:**
- Core-level checks of init responses, exit codes, other Postgres follow-ups.

## Decisions

### Log and rethrow
Replace `return Response.Fail(statusCode: "data clean failed");` with `throw;`. This is the decision already recorded in the archived change `2026-10-28-fix-postgres-delete-all-data` (design section "Log and rethrow") and matches `InitDb` and `BulkInsert` in the same client. Nothing else in the method changes.

### Failure-path test inside `PostgresNoSqlTests`
Add `DeleteAllData_Throws_When_Database_Is_Unreachable` to `PostgresNoSqlTests`: build the props with the file's `CreateProps()` helper and override only `postgres.host` to `127.0.0.1` and `postgres.port` to `1` (2026-11-16, user: reuse `CreateProps()` as the code reviewer suggested, so the test states only what differs) and assert `await Assert.ThrowsAnyAsync<NpgsqlException>(() => client.DeleteAllData())`. No service listens there, so the connection is refused: immediately on Linux, after about 2 s on Windows, well within Npgsql's 15 s connect timeout. `127.0.0.1` avoids a second (IPv6) attempt that `localhost` would add on Windows. The constructor does not connect (`NpgsqlDataSource.Create` is lazy), so the first network call is inside `DeleteAllData`. The test must call only the new client's `DeleteAllData()`, not `CreateClient()` or `InitDb()`. It needs no database and runs no SQL.

- A refused connection is not the realistic production failure: in a real run an unreachable database already stops at `InitDb`, which rethrows. Failures that only `DeleteAllData` meets are, for example, a missing TRUNCATE privilege or a lock timeout. The refused connection is a cheap, database-free way to prove the method propagates errors; the spec's "Failed reset" scenario lists "a database connection cannot be opened" among its examples.
- It goes in `PostgresNoSqlTests`, not a new class, because the client's `NpgsqlDataSource` is static: a separate class could run in parallel with the live tests and replace their data source. The archived change's objection to a failure-path test applied to that separate-class case; it does not apply here: no other test class creates the client (verified by search), xUnit runs one class's tests one at a time, and every other test builds a new client first, which resets the data source.
- `NpgsqlException` is Npgsql's public type for connection failures (the file already has `using Npgsql;`); accepting any `Exception` would let an unrelated bug (for example a `NullReferenceException` while building the client) pass the test.
- CI runs this test in the Postgres step (its filter `NBomber.YCSB.Tests.PostgresNoSqlTests` matches by substring); it does not depend on the container.
- **Alternative: assert on stderr output**: rejected; brittle and not needed for the requirement.

### Destructive operations
None added. The success path still runs `TRUNCATE` on `ycsb_table`, as today. The new test connects to an unused port and runs nothing.

## Risks / Trade-offs

- [Something listens on port 1] → It does not speak the Postgres protocol, so `DeleteAllData` still throws an `NpgsqlException` and the test still passes correctly.
- [Each new client leaves its previous `NpgsqlDataSource` undisposed] → Pre-existing; the extra one from this test opens no connections.
- [The new test lives in a live-test class that CI runs with Postgres] → It does not depend on the container and does not touch it.
- [A failed cleanup now stops the run] → Intended; it is what the spec requires.

## Migration Plan

No migration. Rollback is reverting the line.
