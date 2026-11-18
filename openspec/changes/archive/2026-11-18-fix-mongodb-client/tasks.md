# Tasks

## 1. MongoDB client (MongoDb folder only)

- [x] 1.1 `src/NBomber.YCSB/MongoDb/MongoDbHelper.cs`: make the class `public`; flat `BuildDocument(key, values)` (no timestamps); add `BuildScanFilter`, `BuildProjection`, `BuildUpdate`; `BuildFieldsDocument` maps null values to empty strings (design.md)
- [x] 1.2 `src/NBomber.YCSB/MongoDb/MongoDbYcsbClient.cs`: `WriteConcern.W1`; Insert/BulkInsert/Read/Scan/Update/ReadModifyWrite as in design.md (`$gte` scan with sort and limit, projections by plain field name, `Fail("no data")` on misses and empty scans, no `updatedAt`)

## 2. Tests

- [x] 2.1 New offline `tests/NBomber.YCSB.Tests/MongoDbHelperTests.cs`: flat document; scan filter `{_id: {$gte: key}}`; projection null/empty → null, `{field3}` → `{field3: 1}`; update is `$set` of top-level fields only
- [x] 2.2 Live tests in `tests/NBomber.YCSB.Tests/MongoDbTests.cs` (**require explicit approval to run**; unique keys): scan returns up to count records in `_id` order from the start key; projected read returns the field; read/RMW of a missing key returns "no data"; update preserves other fields and adds no timestamp fields

## 3. Build and regression

- [x] 3.1 `dotnet build NBomber.YCSB.sln`: 0 errors, no new warnings in touched files (run by the Team Lead)
- [x] 3.2 Offline tests including `MongoDbHelperTests` pass (run by the Team Lead)
- [x] 3.3 Live `MongoDbTests` (**requires explicit approval to run**)
