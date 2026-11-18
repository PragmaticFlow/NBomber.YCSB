# Design

## Context

`ScyllaYcsbClient` builds the cluster with `AddContactPoints`, `WithPort` and optional `WithCredentials`; no consistency is set anywhere, so `ScyllaDBCSharpDriver` 3.22 uses its default (`LOCAL_ONE`). Java YCSB sets QUORUM on each prepared statement.

## Goals / Non-Goals

- Goals: QUORUM for every data statement with the smallest change.
- Non-goals: per-operation or configurable levels; LWT; `local_dc`; Core changes.

## Decisions

### Builder-level default
```csharp
var builder = Cluster.Builder()
    .AddContactPoints(hosts)
    .WithPort(port)
    // Java YCSB scylla binding default: QUORUM for reads and writes
    .WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.Quorum));
```
One call covers every bound statement, including lazily prepared ones in the three caches and the `?? Prepare*()` fallbacks; per-statement settings would touch four places and risk missing one. DDL and TRUNCATE are unaffected (schema agreement / all nodes required regardless).

### Live tests
`RAW_SESSION` in `ScyllaDbTests` gets the same `.WithQueryOptions(...)` so read-back after a client write is deterministic; the `scylladb.host` prop name becomes `scylladb.hosts` (the client reads `scylladb.hosts`; it worked only via the localhost default).

### Destructive operations
None new.

## Risks / Trade-offs

- [Scylla results change] → Intended; documented in CLAUDE.md.
- [Single node with RF 3 fails with UnavailableException] → Documented: use `-p scylladb.replicationfactor=1` and drop an old RF-3 keyspace.
- [Bulk load at QUORUM with 512 concurrent inserts on small containers] → Slower init and higher WriteTimeout risk; watch in the live run; not changed here.

## Migration Plan

None. Rollback is reverting the change.
