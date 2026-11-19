# Proposal

## Why

`PostgresNoSQLYcsbClient` repeats the response-size formula `UTF8(key) + UTF8(json)` six times (Insert, Read, Scan, ReadModifyWrite twice, Update). The other backends keep this in their static helper (`RedisHelper.GetSize`, `ScyllaDbHelper.GetSize`, `MongoDbHelper.GetSize`, `RavenDbHelper.GetSize`), where it can be unit tested offline. Moving it into `PostgresNoSQLHelper` aligns Postgres with that pattern.

## What Changes

- Add `PostgresNoSQLHelper.GetSize(string key, string json)` that returns the UTF-8 byte count of the key plus the JSON value, as a `long`.
- Replace the six inline calculations in `PostgresNoSQLYcsbClient` with calls to it, and drop the now-unused `using System.Text;` there.
- Add offline unit tests for `GetSize` in `PostgresNoSQLHelperTests`.

Pure refactor: no SQL, round trip, response status or reported `sizeBytes` changes.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
None. Observable behaviour is unchanged, so the change sets `skip_specs: true`.

## Impact

- Affected database: PostgreSQL only (`src/NBomber.YCSB/PosgresNoSQL/`). Redis, MongoDB, RavenDB and ScyllaDB are out of scope.
- Out of scope: changing what size is reported (for example counting field names or the JSONB storage size), and any other Postgres client refactoring.
- Benchmark results: unchanged. `sizeBytes` per operation is byte-for-byte identical, so results stay comparable with earlier Postgres runs and other databases.
- Tests: `tests/NBomber.YCSB.Tests/PostgresNoSQLHelperTests.cs` (offline).
