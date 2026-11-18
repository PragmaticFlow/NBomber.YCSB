# Tasks

## 1. PostgreSQL client (PosgresNoSQL folder only)

- [x] 1.1 New `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLHelper.cs` (public static): `BuildConnectionString(props)` with existing values plus `MaxAutoPrepare = 20`, `AutoPrepareMinUsages = 5`; `BuildProjection(fields, parameters)` (design.md)
- [x] 1.2 `src/NBomber.YCSB/PosgresNoSQL/PostgresNoSQLYcsbClient.cs`: use the helper for the data source and for projections in Read, Scan and the RMW read; InitDb without `CREATE INDEX` (user decision 2026-11-18: no `DROP INDEX` in InitDb; the legacy index is removed by the reset in scope-delete-all-data); Update and RMW write return `Fail("no data")` when 0 rows are affected; keep the `||` merge SQL and pool settings unchanged

## 2. Tests

- [x] 2.1 New offline `tests/NBomber.YCSB.Tests/PostgresNoSQLHelperTests.cs`: connection string has `MaxAutoPrepare > 0`, `AutoPrepareMinUsages = 5`, `MinPoolSize = 1`, `MaxPoolSize = 100`; `BuildProjection` gives the same SQL text for `{field1}` and `{field7}` and adds parameter `f0` with the field name
- [x] 2.2 `tests/NBomber.YCSB.Tests/PostgresNoSqlTests.cs` (**live, require explicit approval to run**): invert the GIN-index assertion (primary key stays); (the legacy-index test moves to scope-delete-all-data as a reset test); update and RMW of a missing key return "no data"; projected read succeeds with sizeBytes > 0

## 3. Build and regression

- [x] 3.1 `dotnet build NBomber.YCSB.sln`: 0 errors, no new warnings in touched files (run by the Team Lead)
- [x] 3.2 Offline tests including `PostgresNoSQLHelperTests` pass (run by the Team Lead)
- [x] 3.3 Live `PostgresNoSqlTests` (**requires explicit approval to run**)
