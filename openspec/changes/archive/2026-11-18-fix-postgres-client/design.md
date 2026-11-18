# Design

## Context

`PostgresNoSQLYcsbClient` (Npgsql 10.0.1, `NpgsqlDataSource`, pooled connections opened per call) creates a GIN index in `InitDb`, never prepares statements, inlines projected field names into SQL and ignores affected-row counts on update.

## Goals / Non-Goals

- Goals: YCSB schema; server-side prepared statements like Java; `Fail("no data")` on update misses; minimal diff inside `PosgresNoSQL/*`.
- Non-goals: pool settings; BulkInsert concurrency; static data source; `-p` options for preparation; Core or `IDbYcsbClient` changes.

## Decisions

### InitDb
Keep the create-if-missing table step without `CREATE INDEX`. A legacy `idx_ycsb_table_jsonb` is removed by the reset (`DeleteAllData` drops and recreates the table; change scope-delete-all-data). User decision 2026-11-18: no `DROP INDEX` in InitDb.

### Automatic preparation
`PostgresNoSQLHelper.BuildConnectionString(props)` returns the `NpgsqlConnectionStringBuilder` with the existing values (host, port, database, user, password, `MinPoolSize = 1`, `MaxPoolSize = 100`) plus `MaxAutoPrepare = 20`, `AutoPrepareMinUsages = 5`. Npgsql prepares per physical connector after 5 uses of the same SQL text; prepared statements survive pooled close/reopen. ~7 distinct texts after parameterization, so 20 avoids eviction. No explicit `Prepare()` calls.

### Parameterized projection
```csharp
// "jsonb_build_object(@f0, ycsb_value -> @f0, ...)"; adds text parameters f0..fN
public static string BuildProjection(IEnumerable<string> fields, NpgsqlParameterCollection parameters)
```
Used by Read, Scan and the RMW read (removes the three duplicated selector blocks). Parameters are `text`; add `::text` casts if type resolution needs them.

### Update misses
`var rows = await cmd.ExecuteNonQueryAsync(); if (rows == 0) return Response.Fail(statusCode: "no data");` in Update and the RMW write phase. SQL text (`||` merge) unchanged.

### Hot path
Fewer server parses/plans; one parameter more for projected queries; no new allocations of note.

### Destructive operations
None in this change. (`DeleteAllData` dropping and recreating the table is change scope-delete-all-data.)

## Risks / Trade-offs

- [Postgres results change] → Intended; documented in CLAUDE.md; earlier Postgres results not directly comparable.
- [First 5 executions per statement per connector are unprepared] → Negligible; same as pgjdbc.
- [Parameterized projection] → Planner sees a parameter; irrelevant for primary-key lookups; covered by live projection tests.

## Migration Plan

None. The legacy index is dropped automatically on the next run. Rollback is reverting the change.
