## Purpose

Defines the observable behaviour of the ScyllaDB backend of the YCSB benchmark that affects comparability with the Java YCSB `scylla` binding.

## ADDED Requirements

### Requirement: QUORUM consistency
Every read, scan, insert and update SHALL run at consistency level QUORUM, the Java YCSB default, so that latency is comparable with Java YCSB and a read sees every write acknowledged before it.

#### Scenario: Three-node cluster with replication factor 3
- **WHEN** a workload runs on a 3-node cluster with replication factor 3
- **THEN** each write is acknowledged by 2 replicas and each read consults 2 replicas

#### Scenario: Single node
- **WHEN** a workload runs on a single node with `-p scylladb.replicationfactor=1`
- **THEN** QUORUM is one replica and the workload succeeds
