# Proposal

## Why

A review of `PostgresNoSQLYcsbClient` against the Java YCSB `postgrenosql` binding found:

- **High:** `InitDb` creates a GIN index on the JSONB column. It is not in YCSB's schema (README: primary key + JSONB only) and no query uses it (reads/scans filter by primary key), but every insert, update and the bulk load must maintain it. Existing tables keep it (named Docker volume; table created only if missing).
- **Medium:** no prepared statements — SQL text is built per call and parsed/planned every time. Java caches `PreparedStatement`s (pgjdbc prepares server-side after 5 executions). Field names are inlined into projected SQL, giving 3×(fieldcount+1)+2 distinct texts.
- **Low:** Update and the RMW write ignore the affected row count and return OK on a missing key; Java returns UNEXPECTED_STATE; our contract requires `Fail("no data")`.

The JSONB merge update (`||`) already matches Java and stays. A database-client fix is sufficient; shared Core is unchanged.

## What Changes

- `InitDb` no longer creates the GIN index. A legacy index is removed by the reset, which recreates the table (change scope-delete-all-data).
- Npgsql automatic preparation: `MaxAutoPrepare = 20`, `AutoPrepareMinUsages = 5` (pool settings unchanged: `MinPoolSize = 1`, `MaxPoolSize = 100`).
- Projected field names are passed as SQL parameters, so projected statements have one SQL text regardless of the field (user decision).
- Update and RMW return `Fail("no data")` when no row matches.
- New small public `PostgresNoSQLHelper` with the connection-string and projection builders (offline-testable).
- Affected databases: **PostgreSQL only**.
- **Out of scope**: pool settings, BulkInsert concurrency, the static `NpgsqlDataSource` gotcha, scan reading full values (kept; more faithful than Java's key-only scan).

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `postgres-client`: adds schema, prepared-statement and update-miss requirements. Existing reset requirements unchanged.

## Impact

- Code: `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLYcsbClient.cs`, new `PostgresNoSQLHelper.cs`. Tests: new offline `PostgresNoSQLHelperTests.cs`; `PostgresNoSqlTests.cs` (GIN assert inverted, new live tests).
- **Benchmark results change for PostgreSQL**: lower insert/update/RMW latency (no GIN maintenance), lower latency for all operations (prepared statements), faster bulk load. Earlier Postgres results are not directly comparable; results move closer to Java YCSB.
