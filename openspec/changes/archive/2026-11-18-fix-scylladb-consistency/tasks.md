# Tasks

## 1. ScyllaDB client (ScyllaDb folder only)

- [x] 1.1 `src/NBomber.YCSB/ScyllaDb/ScyllaYcsbClient.cs`: add `.WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.Quorum))` to the cluster builder with a one-line comment (design.md); nothing else

## 2. Tests

- [x] 2.1 `tests/NBomber.YCSB.Tests/ScyllaDbTests.cs`: `RAW_SESSION` uses the same QUORUM query options; `scylladb.host` → `scylladb.hosts`. No new offline tests (one builder line)

## 3. Build and regression

- [x] 3.1 `dotnet build NBomber.YCSB.sln`: 0 errors, no new warnings in touched files (run by the Team Lead)
- [x] 3.2 Offline tests pass (run by the Team Lead)
- [x] 3.3 Live `ScyllaDbTests` on the project's 3-node compose (**requires explicit approval to run**; drops and recreates `ycsb_table`)
