# Proposal

## Why

Java YCSB's `scylla` binding (`ScyllaCQLClient.java` lines 62-63, set per statement for read, scan, update, insert and delete; README: "Default value is `QUORUM`") uses **QUORUM** for reads and writes. Our `ScyllaYcsbClient` sets no consistency, so the driver default applies (`ScyllaDBCSharpDriver` 3.22, a DataStax C# driver fork: `LOCAL_ONE`). With replication factor 3 (our default and the 3-node compose):

- writes wait for 1 replica instead of 2 and reads touch 1 replica instead of 2, so latency and throughput look better than Java YCSB's;
- R + W ≤ RF, so a read can miss a just-acknowledged write (relevant to workload D).

Load balancing (token-aware) and scan semantics (`TOKEN(key) >= TOKEN(?) LIMIT ?`) already match Java. A client fix is sufficient; shared Core is unchanged.

## What Changes

- The cluster builder sets the default consistency to QUORUM for every statement: `.WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.Quorum))`. No new options (user rule).
- Live tests: the raw verification session uses QUORUM too, and the `scylladb.host` prop typo becomes `scylladb.hosts`.
- Affected databases: **ScyllaDB only**.
- **Out of scope**: consistency `-p` options, LWT, `local_dc`, BulkInsert concurrency (512).

## Capabilities

### New Capabilities
- `scylladb-client`: consistency level of the ScyllaDB backend.

### Modified Capabilities
<!-- None. -->

## Impact

- Code: `src/NBomber.YCSB/ScyllaDb/ScyllaYcsbClient.cs` (one builder call). Tests: `tests/NBomber.YCSB.Tests/ScyllaDbTests.cs`.
- **Benchmark results change for ScyllaDB**: higher read/update/insert latency, lower throughput; reads see acknowledged writes. Earlier Scylla results are not directly comparable; results become comparable with Java YCSB defaults.
- **Single node**: a single node with the default replication factor 3 can no longer serve QUORUM (needs 2 replicas). Use `-p scylladb.replicationfactor=1`, and drop an existing keyspace created with RF 3 first (`CREATE KEYSPACE IF NOT EXISTS` keeps it).
