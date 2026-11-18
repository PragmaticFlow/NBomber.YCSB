# Proposal

## Why

A review of `MongoDbYcsbClient` against the Java YCSB `mongodb` binding (`MongoDbClient.java`) found:

- **Critical:** Scan filters `_id == startKey` (`MongoDbHelper.BuildFilter`), so it returns at most 1 document. Java uses `{_id: {$gte: startKey}}`, sort `_id` ascending, limit N. All MongoDB workload E results are invalid.
- **High:** values are stored under a nested `fields` sub-document, but projections include top-level `fieldN`, so Read, Scan and ReadModifyWrite with `readallfields=false` return only `_id`.
- **Medium:** Read on a missing key throws (`GetSize(null)`) instead of returning `Response.Fail(statusCode: "no data")`; Java returns NOT_FOUND.
- **Low:** extra `createdAt`/`updatedAt` fields (insert) and an extra `$set updatedAt` on every update, plus `DateTime.UtcNow` on the hot path; Java has neither.
- Write concern: Java's default URL uses `w=1`; we use the driver default (server default `w:majority` on 5.0+), equal on a standalone server but slower on a replica set.

A database-client fix is sufficient; shared Core is unchanged.

## What Changes

- Documents become flat like Java YCSB: `{ _id, field1..fieldN }` (user decision). No timestamps.
- Scan: `_id >= startKey`, sort `_id` ascending, limit `count`; `Fail("no data")` when no document is found.
- Projections use the plain field names; Read/RMW return `Fail("no data")` on a miss.
- Update/RMW: `$set` of the given fields only (partial update preserved).
- `MongoClientSettings.WriteConcern = WriteConcern.W1` (user decision).
- `MongoDbHelper` becomes `public` with small builders (document, scan filter, projection, update) for offline tests.
- Affected databases: **MongoDB only**.
- **Out of scope**: `DeleteAllData` scope (drops every non-system database; separate approval), BSON Binary vs string values, collection name.

## Capabilities

### New Capabilities
- `mongodb-client`: observable behaviour of the MongoDB backend (document layout, scan, projection, misses, partial updates, write concern).

### Modified Capabilities
<!-- None. -->

## Impact

- Code: `src/NBomber.YCSB/MongoDb/MongoDbHelper.cs`, `MongoDbYcsbClient.cs`. Tests: new `tests/NBomber.YCSB.Tests/MongoDbHelperTests.cs` (offline); live tests in `MongoDbTests.cs`.
- **Benchmark results change for MongoDB**: workload E scans read up to maxscanlength documents instead of 1; projected reads return real data (sizeBytes rises); documents are ~50 bytes smaller; misses report "no data" instead of an exception. Earlier MongoDB results are not directly comparable; results become comparable with Java YCSB and the other backends.
