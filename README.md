The **Yahoo! Cloud Serving Benchmark (YCSB)** is an open-source database benchmarking suite and a critical analytical component of cloud-based database management system (DBMS) evaluation. It allows users to comparatively measure how various modern SQL and NoSQL DBMS perform simple database operations on generated datasets.

The original benchmark was developed by workers in the research division of Yahoo! who released it in 2010 with the stated goal of "facilitating performance comparisons of the new generation of cloud data serving systems", particularly for transaction-processing workloads which differed from ones measured by benchmarks designed for more traditional database management systems.

The strength of YCSB benchmarks is that they can be used to compare databases that are architecturally distinct and measure the way various database configurations perform under different workloads.

The YCSB framework also automates or simplifies essential benchmarking process tasks such as:
- Defining core workloads with essential parameters
- Connecting to the database with the database drivers
- Executing the workload on the database
- Collecting and storing performance data
- Creating a new interface layer for benchmarking

## Supported databases

Each backend has a `docker-compose.yml` in its folder under `src/NBomber.YCSB/`. Start one stack at a time; Docker needs enough memory for that stack (see the notes below).

| `--db` | Compose file | Default props | `DeleteAllData` removes |
|---|---|---|---|
| `redis` | `Redis/` | `redis.host=localhost`, `redis.port=6379`, `redis.password` | the whole Redis database (FLUSHDB) |
| `keydb` | `KeyDb/` | `keydb.host=localhost`, `keydb.port=6380`, `keydb.password` | the whole KeyDB database (FLUSHDB) |
| `mongodb` | `MongoDb/` | see `MongoDbYcsbClient` | the `ycsb` database |
| `postgres` | `PosgresNoSQL/` | `postgres.host/port/database/user/password` | table `ycsb_table` |
| `cockroachdb` | `CockroachDb/` | `cockroachdb.host=localhost`, `.port=26257`, `.database=defaultdb`, `.user=root`, `.password` | table `ycsb_table` in `defaultdb` |
| `tidb` | `TiDb/` | `tidb.host=localhost`, `.port=4000`, `.database=ycsb`, `.user=root`, `.password` | table `ycsb.ycsb_table` |
| `ravendb` | `RavenDb/` | see `RavenDbYcsbClient` | the RavenDB database (hard delete) |
| `scylladb` | `ScyllaDb/` | `scylladb.hosts/port/keyspace/user/password/replicationfactor` | table `ycsb_table` (keyspace kept) |
| `cassandra` | `CassandraDb/` | `cassandra.hosts=localhost`, `.port=9043`, `.keyspace=ycsb`, `.replicationfactor=1`, `.user`, `.password` | table `ycsb_table` (keyspace kept) |
| `aerospike` | `AerospikeDb/` | `aerospike.host=localhost`, `.port=3000`, `.namespace=test`, `.set=ycsb_table`, `.timeout=10000`, `.maxconcurrency=512` | the records of set `ycsb_table` (truncate) |

Example:

```bash
docker compose -f src/NBomber.YCSB/TiDb/docker-compose.yml up -d --wait
dotnet run --project src/NBomber.YCSB -- run --workload A --db tidb --warmup 3
```

Every database has its own client in its own folder, implementing `IDbYcsbClient` directly. KeyDB, Cassandra and CockroachDB clients are deliberate copies of the Redis, ScyllaDB and PostgreSQL client logic (same commands, queries, consistency and size accounting), so that results for each pair compare the servers rather than different client code. The copies are independent: a change to one client does not change the other.

### Benchmark limitations

The compose stacks are small single-node deployments for local testing. Their results describe those containers, not the products in a production configuration, and differ from each other in replication, isolation and protocol, so compare them with care.

**Redis and KeyDB** share the same client logic. Differences from the Java YCSB `redis` binding:
- Scan returns keys in lexicographic order, starting at the start key (inclusive): every member of the `_indices` sorted set has score 0 and is read by value range. The Java binding scores members by `String.hashCode()`, so its scan follows hash order, unrelated to the start key.
- Scan sends its per-key hash reads concurrently on the shared connection instead of one at a time as Jedis does, so scan latency is not comparable with published Java Redis results.
- Each client uses one shared `ConnectionMultiplexer` for all threads; the Java binding opens one connection per thread. Operation latency includes queueing in the client.
- Records are stored under `hash:<key>`; the Java binding uses the bare key.
- A projected read whose requested fields are all missing returns "no data" (Java returns OK), and read-modify-write skips the write when its read misses (Java writes anyway). Update of a missing key creates a hash that is not in the index and returns OK, as in Java. Workloads A-F do not reach these cases.
- Size accounting: full reads count the stored key (with `hash:`), field names and values; projected reads and scans with a field list count values only; scans count keys without the `hash:` prefix.
- Both compose stacks run with `--appendonly yes`. After a large initial load, background AOF rewriting and fsync may overlap the measured phase; use `--warmup` or start measuring after the load settles.
- `DeleteAllData` flushes the whole default database (`FLUSHDB`); do not point the benchmark at a shared server.

**KeyDB** has its own client with the same logic and data model as the Redis client. KeyDB runs with `--server-threads 2`, but StackExchange.Redis uses one multiplexed connection, so the extra server thread is probably not exercised: results are not a multi-core KeyDB benchmark. KeyDB 6.3.4 is a different server generation from the Redis 8.4 image.

**Cassandra** has its own client with the same logic as the ScyllaDB client: QUORUM for every operation, prepared statements, one column per field. The compose stack is one node with replication factor 1 and a 512 MB heap (1.5 GB container limit), so large concurrent loads may hit write timeouts. `cassandra.replicationfactor` only takes effect when the client creates the keyspace; to change it, drop the existing `ycsb` keyspace first. With the defaults (one node, RF 1), results are not comparable with ScyllaDB's defaults (three nodes, RF 3); compare them with matching node counts and replication factor. Scan follows token order (the hash of the primary key), not lexicographic primary-key order, so it returns the rows whose tokens follow the start key's token; this matches the Java cassandra-cql binding and ScyllaDB. Update and read-modify-write create a missing key (Cassandra upsert). `DROP TABLE` leaves snapshots on disk; reclaim with `docker exec cassandra nodetool clearsnapshot --all`.

**CockroachDB** has its own client with the same JSONB logic as the PostgreSQL client, over the PostgreSQL wire protocol (Npgsql auto-prepare). The one intended difference is the initial load: it inserts 1000-row chunks with at most 8 in parallel, where the PostgreSQL client starts all chunks at once. It is a single insecure node with capped memory (`--cache=128MiB --max-sql-memory=128MiB`, 1 GB container), SERIALIZABLE isolation and no client-side retries, so a transaction-restart error (40001) would count as a failed operation; none was observed in tests with 32 threads on hot keys. Dropped tables and MVCC garbage stay on disk until garbage collection (`gc.ttlseconds`, 4 hours by default).

*Licensing:* the compose file pins `cockroachdb/cockroach:v26.4.1`, which is under the CockroachDB Software License. Cockroach Labs [documents](https://www.cockroachlabs.com/docs/stable/licensing-faqs) that single-node clusters started with `cockroach start-single-node` need no licence key and are not throttled, for design, prototyping, testing and development use in non-production, internal environments. Otherwise an unlicensed cluster is throttled to 5 concurrent open SQL transactions 7 days after initialisation. The throttle state is not visible from the server and passing tests do not prove it is absent. Before publishing performance numbers, confirm the exemption applies to your use, or set a licence key (for example Enterprise Free: `SET CLUSTER SETTING enterprise.license = '<key>'`). Do not publish results from a possibly throttled cluster without saying so, and do not recreate the Docker volume to start a new grace period.

**TiDB** uses MySqlConnector with one `TEXT` column per field and a clustered primary key on a binary-collated key (scan is in byte order). The compose stack is a constrained single node (PD, TiKV, TiDB, about 2.3 GB of memory; TiKV needs 1.5 GB) with small TiKV thread pools and cache, and no replication. Notes:
- Autocommit writes are optimistic in TiDB 8.5 (`pessimistic-auto-commit=false`); TiDB retries conflicting statements itself, and no write conflicts reached the client at 64 threads on hot keys. Much heavier contention could still surface them as failed operations; the client does not retry.
- Statements use MySqlConnector's text protocol (parameters interpolated on the client, no server-side prepared statements or plan cache), unlike PostgreSQL and CockroachDB, which use Npgsql auto-prepare.
- The connection pool opens connections on demand. On a fresh stack at 64 threads some first operations failed with "Connect Timeout expired". Use `--warmup` for warmed-up runs, but warm-up does not guarantee that every pooled connection is open before measurement.
- `DeleteAllData` drops the table, but TiDB keeps the data until garbage collection (`tidb_gc_life_time`, 10 minutes by default). Repeated large runs increase disk usage and background GC activity.
- The initial load inserts 500 rows per statement; very large `fieldcount` × `fieldlength` values could exceed TiDB's entry or transaction size limits (an error, not silent data loss). SQL text is built per operation and the data source is not disposed; both are negligible for a one-shot run.

**Aerospike** uses the official async client with bins `field1..N`. It has no ordered primary-key range access, so Scan returns `Fail("scan not supported")` and `--db aerospike --workload E` is rejected. Data is served from memory (`storage-engine memory`) with a backing file inside the container; there is no volume or replication, so data is lost when the container is removed, and write latency is not comparable with stacks that persist writes to disk before acknowledging; with the default field sizes the 4 GB container fits about 1.4M records (Docker Desktop's VM memory also limits it). Insert is create-only, like the Java binding, so a duplicate key is a failed operation. Update and read-modify-write are update-only: they merge fields and return "no data" on a missing key. The Java binding's update uses replace-only, which drops unrelated fields; we keep YCSB partial-update semantics. The bulk load uses replace. `aerospike.maxconcurrency` (default 512 when not set; any other value must be a positive integer or the run fails) bounds commands in flight, connections per node and bulk-load parallelism; a `--threadcount` above it queues commands on the client (the wait counts as latency, and small values may also cause timeouts); report a non-default value with results. Operations use a 10 s timeout with no retries; in tests under heavy host load the initial bulk load sometimes timed out, which was not reproduced in isolation and is not conclusively explained. `DeleteAllData` truncates the set and the server finishes the purge in the background.

**Size accounting** differs by data model: PostgreSQL and CockroachDB count the key and JSON text, the other backends count the key, field names and values (Redis and KeyDB with the exceptions listed above).

## Warm-up

By default there is no warm-up, as in YCSB: the database receives exactly `operationcount` operations, all measured. Use `--warmup <seconds>` to add a warm-up before measurement. Warm-up operations are extra, are not measured and change data, and because warm-up is time-capped a faster database runs more of them.

Results produced before this option existed used a fixed 3-second warm-up and may not be directly comparable. `--warmup 3` reproduces that behaviour.

## Links

- [Our project docs](https://nbomber.com/docs/load-testing-databases/overview)
