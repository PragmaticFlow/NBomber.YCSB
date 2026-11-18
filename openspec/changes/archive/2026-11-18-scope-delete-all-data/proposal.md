# Proposal

## Why

The user wants clear responsibilities for the two init steps of every database client, with the scenario orchestrating them:

- `DeleteAllData` deletes the benchmark data (for table-based stores: drops the benchmark table);
- `InitDb` creates what the run needs (keyspace/table/database, prepared statements);
- `YcsbScenario` decides the order: **reset first, then initialise, then load**.

Revision (2026-11-18, user): the previous version of this change made Scylla use one `RecreateTable()` in both methods and made Postgres `DeleteAllData` drop *and* recreate. The user rejected the mixed responsibilities: no `RecreateTable`, the two functions separated, the scenario orchestrates.

Today the scenario runs `InitDb → DeleteAllData → BulkInsert`, so a `DeleteAllData` that drops a table would leave nothing to load into. The order therefore changes to `DeleteAllData → InitDb → BulkInsert` (user decision).

Other findings kept from the first version: MongoDB `DeleteAllData` dropped **every non-system database** on the server (now only `ycsb`).

## What Changes

- **YcsbScenario (Core)**: init order becomes `DeleteAllData` → `InitDb` → `BulkInsert`.
- **ScyllaDB**: `DeleteAllData` = `DROP TABLE IF EXISTS ycsb_table` (succeeds when the keyspace does not exist); `InitDb` = create keyspace if missing, `CREATE TABLE` with columns from `fieldcount`, clear statement caches, prepare and warm up statements. No `RecreateTable()`.
- **PostgreSQL**: `DeleteAllData` = `DROP TABLE IF EXISTS ycsb_table` only (no recreate); `InitDb` = `CREATE TABLE IF NOT EXISTS` (YCSB schema, no index).
- **MongoDB**: `DeleteAllData` drops only the `ycsb` database; `InitDb` unchanged (nothing to create).
- **Redis**: unchanged (`FLUSHDB`; `InitDb` does nothing) — order-independent.
- **RavenDB**: `DeleteAllData` hard-deletes the benchmark database (no error when absent); `InitDb` creates it and warms up the connection (code moved, not added).
- No `IDbYcsbClient` change.
- **Out of scope**: Redis `FLUSHDB` scope; RavenDB delete scope beyond what the order change needs; the open changes `fix-mongodb-client`, `fix-postgres-client`, `fix-scylladb-consistency` (this change stacks on them, same branch, archived after them).

## Capabilities

### New Capabilities
- `ravendb-client`: reset and initialisation of the RavenDB backend.
<!-- mongodb-client and scylladb-client are introduced by the open changes fix-mongodb-client and fix-scylladb-consistency; archive this change after them. -->

### Modified Capabilities
- `core-workload`: adds the init order requirement (reset, then initialise, then load).
- `postgres-client`: "Reset benchmark data" and "Reset is limited to the benchmark table" — reset drops the table, initialisation creates it.
- `mongodb-client`: adds the reset-scope requirement.
- `scylladb-client`: adds the reset/initialisation requirement.

## Impact

- Code: `src/NBomber.YCSB/YcsbScenario.cs` (two lines swapped), `ScyllaDb/ScyllaYcsbClient.cs`, `PosgresNoSQL/PostgresNoSQLYcsbClient.cs`, `MongoDb/MongoDbYcsbClient.cs`, `RavenDb/RavenDbYcsbClient.cs` (reset/init split), `Infra/IDbYcsbClient.cs` (doc comments only). Tests: live tests that call `DeleteAllData` and then expect a usable table now call `InitDb` after it. Docs: `CLAUDE.md` (init order, cleanup list).
- **Benchmark results: no change** — reset and init run before the measured load; the loaded data and schema are the same.
- **Safety**: MongoDB cleanup no longer touches other databases on the server.
