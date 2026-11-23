# Proposal

## Why

`CassandraYcsbClient`/`CassandraHelper` carry avoidable duplication:
- two nearly identical field-extraction helpers;
- a `null!` suppression in the insert bindings;
- the same full/projected read code written twice (in `Read` and `ReadModifyWrite`);
- a `BulkInsert` that creates one task per record up front.

Simplifying them makes the client easier to maintain, without changing YCSB behaviour.

## What Changes

In the Cassandra folder only (`src/NBomber.YCSB/CassandraDb/`):
- **Field extraction:** merge `ExtractAllFields` and `ExtractFields` into one `ExtractFields(Row, IReadOnlyList<string>, int columnOffset)` (same order, offsets and null skipping), and update all call sites.
- **Insert bindings:** `BuildInsertBindings` returns `object?[]` and binds missing fields as `null` without `null!`. The positional order is unchanged.
- **Reads:** one private read method in the client, used by `Read` and `ReadModifyWrite`. It covers choosing the full or projected statement, executing it, the "no data" check and extracting the fields. Size accounting is unchanged.
- **Bulk load:** `BulkInsert` uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` = the existing 512 and the same prepared INSERT, as `AerospikeYcsbClient.BulkInsert` does. Only up to 512 inserts exist at a time, instead of one pending task per record.
  - **Behaviour on failure changes slightly (approved by the user):** after the first failed insert, no new inserts start, and the first observed exception is rethrown (unwrapped) after the in-flight inserts complete. Today every record is still attempted before the first exception surfaces. Either way one driver exception propagates, the initialisation fails and the run stops; a failed load just ends sooner and leaves fewer rows, which the next `DeleteAllData` drops.
- **Comments:** add the comments that exist only in the ScyllaDB copy (prepared-statement cache, insert/update binding order) to the Cassandra files.
- **Unchanged:** `BuildUpdateBindings`, `BuildFieldsKey`, the prepared-statement caches (eager in `InitDb`, lazy otherwise, eviction on failure), CQL text, consistency, replication factor, timeouts, retries, props and defaults.

ScyllaDB (`ScyllaDbHelper`, `ScyllaYcsbClient`) is **not changed** in this change. The user decided to refactor it separately once its live tests can run again. Until then the Cassandra client intentionally differs structurally from its ScyllaDB original; the measured operations still run the same statements, bindings and size accounting.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
None. This is a refactor with no change to observable operation behaviour, so the change sets `skip_specs: true`. The only behavioural nuance is when an unmeasured bulk-load failure surfaces, which no spec covers.

## Impact

- **Databases:** Cassandra only (`src/NBomber.YCSB/CassandraDb/`). No ScyllaDB or other client change, no `IDbYcsbClient`, Docker, CLI or config change, no test changes expected.
- **Out of scope:** the ScyllaDB refactor (separate follow-up), making the concurrency limit configurable or a `const`, sorting or normalising field keys, CQL changes, and any change to measured operations.
- **Benchmark results:** unchanged. Measured operations run the same statements with the same bindings and size accounting. `BulkInsert` is unmeasured setup.
- **Verification:** build, offline tests, and the live `CassandraTests` under the shared lock script (approved by the user).
