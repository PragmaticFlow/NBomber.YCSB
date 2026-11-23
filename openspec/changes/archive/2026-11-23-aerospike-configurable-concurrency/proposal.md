# Proposal

## Why

The Aerospike client caps concurrency with a hard-coded 512: in-flight async commands, connections per node and BulkInsert parallelism. A run above 512 threads queues commands on the client (counted as latency), and the only way to change the cap is to edit code. Its documentation is also inaccurate: it says the Java YCSB binding overwrites on insert (it uses `CREATE_ONLY` too), does not explain the intentional `UPDATE_ONLY` vs Java `REPLACE_ONLY` difference, and calls the storage "in memory only" although the image's memory engine writes to a backing file inside the container.

## What Changes

- New prop `aerospike.maxconcurrency` (default `512`, must be ≥ 1) sets the async command limit, the per-node connection limit and BulkInsert parallelism together. The default keeps today's behaviour. A zero or negative value is rejected with a clear error before connecting.
- Insert (`CREATE_ONLY`), Update and read-modify-write (`UPDATE_ONLY`, partial merge, `"no data"` on a missing key), BulkInsert (`REPLACE`), timeouts, retries, `MaxCommandAction.DELAY` and the unsupported Scan stay exactly as they are.
- README.md and CLAUDE.md: document the prop and its relation to `--threadcount`, the record-exists policies and the intentional difference from Java YCSB, and correct the storage description. No Docker configuration change.
- Offline tests for the default, a custom value and invalid values; the existing live Aerospike tests already cover partial update, missing-key update, duplicate insert and BulkInsert replace.

## Capabilities

### New Capabilities
- `aerospike-client`: observable behaviour of the Aerospike backend (concurrency setting, operation semantics, unsupported scan), following the existing per-database client specs.

### Modified Capabilities
None.

## Impact

- Database: Aerospike only (`src/NBomber.YCSB/AerospikeDb/`). No other client, no `IDbYcsbClient` change, no Docker change.
- Out of scope: deriving the limit from `--threadcount`, separate limits per setting, retry or timeout changes, scan support, storage engine changes, and the project-wide question of client caps above `--threadcount`.
- Benchmark results: unchanged with the default (512). A run with a different `aerospike.maxconcurrency` changes how many commands the client keeps in flight; results stay comparable only when the setting is reported.
- Tests: `tests/NBomber.YCSB.Tests/AerospikeHelperTests.cs` (offline). Live `AerospikeTests` are unchanged and need approval to run.
