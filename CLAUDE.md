# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

NBomber.YCSB is a .NET 9 console app that implements the Yahoo! Cloud Serving Benchmark (YCSB) on top of NBomber, with backends for Redis, KeyDB, MongoDB, PostgreSQL (JSONB), CockroachDB, TiDB, RavenDB, ScyllaDB, Cassandra and Aerospike. It is a benchmark tool: changes must keep results correct and comparable with the original Java YCSB and across databases. Project docs: https://nbomber.com/docs/load-testing-databases/overview

## Commands

```bash
dotnet build NBomber.YCSB.sln          # NuGet audit warnings NU1900-NU1904 are build errors

# Run a workload (one-shot CLI; the startup banner mentioning "interactive"/"exit" is stale)
dotnet run --project src/NBomber.YCSB -- run --workload A --db redis -p redis.host=localhost -p redis.port=6379

# Offline tests (no database needed)
dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName~DataGeneratorTests|FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests|FullyQualifiedName~ZipfianExtensionsTests|FullyQualifiedName~MongoDbHelperTests|FullyQualifiedName~PostgresNoSQLHelperTests|FullyQualifiedName~KeyDbHelperTests|FullyQualifiedName~CassandraHelperTests|FullyQualifiedName~CockroachDbHelperTests|FullyQualifiedName~TiDbHelperTests|FullyQualifiedName~AerospikeHelperTests"

# Single test
dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName=NBomber.YCSB.Tests.ScanLengthTests.Validator_Accepts_Defaults"

# Live tests for one database (what CI does per backend)
docker compose -f src/NBomber.YCSB/Redis/docker-compose.yml up -d --wait
dotnet test tests/NBomber.YCSB.Tests --filter "NBomber.YCSB.Tests.RedisTests"
```

Live test classes (`<Db>Tests`) need that folder's compose stack running on its default local ports. Their `Workload_*` tests run real workloads, which call `DeleteAllData`, then `InitDb`, first. Cleanup is destructive and differs per backend:
- Redis flushes the whole database.
- MongoDB drops the `ycsb` database only (other databases untouched).
- RavenDB hard-deletes its database.
- Scylla drops `ycsb_table` (keyspace kept); `InitDb` recreates it.
- Postgres drops `ycsb_table` (other tables untouched); `InitDb` recreates it.
- KeyDB flushes the whole KeyDB database (port 6380).
- Cassandra drops `ycsb_table` (keyspace kept, port 9043); `InitDb` recreates it.
- CockroachDB drops `ycsb_table` in `defaultdb`; `InitDb` recreates it.
- TiDB drops `ycsb.ycsb_table` (database kept); `InitDb` recreates it.
- Aerospike truncates set `ycsb_table` in namespace `test` (other sets untouched); the purge finishes in the background.

Only run live tests against the project's own disposable containers, after checking nothing else (another project's container, a WSL service) is serving the port. Use exact `FullyQualifiedName=` filters when running individual live tests.

## Architecture

Flow: `Program.cs` parses the `run` command into `YcsbCliArgs`, validates it with `YcsbCliArgsValidator` (FluentValidation), picks a client in `GetYcsbClient` by `--db`, then calls `YcsbScenario.Run`.

- `YcsbScenario` builds one NBomber scenario. Init runs `DeleteAllData` (removes benchmark data, may drop the table or database), then `InitDb` (creates what the run needs), then `BulkInsert` of `recordcount` generated records. Each iteration picks an operation with `context.Random.Choice` over the workload's weighted operations and runs it as a named step. The load is `IterationsForConstant(threadcount, operationcount)`; `operationcount` is a total across copies, like YCSB. Step names (`read`, `update`, `insert`, `scan`, `read latest`, `read-modify-write`) are asserted by the live tests, so don't rename them. There is no warm-up unless `--warmup <seconds>` is set; warm-up operations are extra, unmeasured and change data.
- `src/NBomber.YCSB/Infra` holds the shared YCSB Core. Keep Core behaviour here, not in database clients:
  - `YcsbCliArgs`: typed options for Core settings (`--fieldcount`, `--minscanlength`, ...), plus `-p key=value` props that are passed to the database client.
  - `Workload.cs`: `WorkloadManager` per-workload presets (operation mix, defaults such as max scan length).
  - `DataGenerator`: keys (`user` + zero padding + number, hashed or ordered), field names and values, key choice, scan length.
  - `IDbYcsbClient`: the only database abstraction.
- One folder per backend (`Redis`, `KeyDb`, `MongoDb`, `PosgresNoSQL` (misspelled, keep it), `CockroachDb`, `TiDb`, `RavenDb`, `ScyllaDb`, `CassandraDb`, `AerospikeDb`), each with a client implementing `IDbYcsbClient`, an optional static helper, and a `docker-compose.yml`. Clients read their connection settings from props via `YcsbCliArgs.TryGet`/`TryParseInt` using `<db>.<setting>` names. Scylla, Cassandra and TiDB also receive `fieldcount` through props (`Program.cs`).
- Every database client is independent: it implements `IDbYcsbClient` directly, with no inheritance, delegation or helper calls into another database's folder. Duplicated code is intentional. Only Infra, `IDbYcsbClient` and driver packages are shared.
- `KeyDbYcsbClient`, `CassandraYcsbClient` and `CockroachDbYcsbClient` (and their helpers) are line-for-line copies of the Redis, ScyllaDB and PostgreSQL logic, differing only in props, names and log text (and CockroachDB's bounded `BulkInsert`: 1000-row chunks, at most 8 in parallel). When you fix or change one of these clients, check its copy and decide per database; keep each pair identical unless a difference is intended, so their results stay comparable.
- Never declare `namespace NBomber.YCSB.Cassandra` or `NBomber.YCSB.Aerospike`: they shadow the driver namespaces `Cassandra` and `Aerospike.Client`. That is why these folders are `CassandraDb` and `AerospikeDb`.
- Client contract: every operation returns NBomber `Response<object>` with `sizeBytes`, or `Response.Fail(statusCode: "no data")` on a miss. Clients never return record data. Update and ReadModifyWrite are partial updates and must preserve unrelated fields.

## Gotchas

- `MessagePack` in `NBomber.YCSB.csproj` is not used by our code. It pins NBomber's transitive dependency to a patched version; do not remove it, or the build fails the NuGet audit.
- `PostgresNoSQLYcsbClient` keeps its `NpgsqlDataSource` in a static field that each new instance replaces, and its `BulkInsert` starts all 1000-row chunks at once (pool exhaustion above ~100k records). Both are known; fixing them is a separate follow-up. `CockroachDbYcsbClient` uses an instance data source and a bounded `BulkInsert`.
- New-backend limitations (details in README "Benchmark limitations"): KeyDB's one multiplexed connection probably does not use its second server thread; Cassandra scans in token order (not lexicographic primary-key order, as in the Java binding) and upserts missing keys, `cassandra.replicationfactor` applies only when the keyspace is created (drop an existing `ycsb` keyspace to change it), and its default RF 1 on one node is not comparable with Scylla's default RF 3 on three nodes; CockroachDB relies on the documented single-node licence exemption from throttling (not verifiable from the server); TiDB uses the text protocol (no server-side prepare), optimistic autocommit retried by TiDB, cold-start connect timeouts without `--warmup`, and delayed GC after `DROP TABLE`; Aerospike has no scan (validator rejects workload E), serves data from memory with a backing file inside the container (lost on container removal), has create-only inserts and update-only (not Java's replace-only) updates, and caps concurrency with `aerospike.maxconcurrency` (default 512).
- Docker memory: run one stack at a time. TiDB needs about 2.3 GB (TiKV 1.5 GB), Aerospike up to 4 GB, Cassandra 1.5 GB.
- ScyllaDB compose: after a Docker restart, `fs.aio-max-nr` resets (re-run `scylla-init-sysctl`) and the nodes can come back with each other's IP addresses, which stops them from starting.
- The Cassandra compose file sets `name: cassandra`, so the project, container and volume (`cassandra_cassandra_data`) keep their names after the move from `Cassandra/` to `CassandraDb/`.
- Offline helper tests (`*HelperTests`, `ScanLengthTests`) are not run by CI, which only filters the per-database live test classes.
- `--exportfile dir/name` writes `dir/name.{csv,html,md,txt}` without a timestamp folder (repeated runs overwrite); a bare name goes to `reports/<timestamp>/`.
- Results produced before `--warmup` existed used a fixed 3-second warm-up and may not be directly comparable with default (no warm-up) runs; `--warmup 3` reproduces the old behaviour.
- Read, update, scan and read-modify-write choose keys with YCSB's scrambled zipfian (0.99) in `DataGenerator.GetKeyZipf`. `recordcount 0` is rejected for all workloads (A-F). Results from before this change are not directly comparable. Read latest (workload D) uses YCSB's Gray zipfian draw with incremental zeta (`DataGenerator.GetKeyLatest`); workload D results from before this change are not directly comparable.
- Known deviations from Java YCSB are tracked as future OpenSpec changes: no acknowledged-insert counter, xxHash instead of FNV, 1-based keys and `field1..N` names, the whole dataset built in memory, RavenDB/Scylla scan semantics.
- Client fixes aligned with the Java bindings; results from before them are not directly comparable for these databases. MongoDB: flat `{ _id, field1..N }` documents, `_id >= startKey` scan, `w:1`. PostgreSQL: no GIN index (a legacy `idx_ycsb_table_jsonb` disappears because `DeleteAllData` drops `ycsb_table`), Npgsql auto-prepare. ScyllaDB: QUORUM consistency for every operation, so a single node needs `-p scylladb.replicationfactor=1` (drop an existing RF-3 `ycsb` keyspace first).

## Workflow (OpenSpec + subagents)

- Changes are spec-driven with OpenSpec (`openspec` CLI, `/opsx:*` commands). `openspec/config.yaml` holds the project context and artifact rules, and `openspec/specs/` the durable capability specs.
- `/backend-task <request>` (`.claude/skills/backend-task/SKILL.md`) orchestrates the full flow: OpenSpec proposal, `architect` review, user approval, `dotnet-developer` implementation, `code-reviewer` review, OpenSpec verify, and archive only after approval.
- Run `/backend-task` with no arguments to resume an active change. Each change's progress, approvals and next step are recorded in `openspec/changes/<name>/workflow-state.md`.
- `architect` and `code-reviewer` are read-only (no Git or CLI). Give them file paths, and save diffs to a temp file for them.

## Code Simplicity and Reuse

- Prefer reusing existing classes, methods, and patterns before introducing new ones.
- Keep implementations simple, readable, and easy to maintain.
- Avoid unnecessary abstractions, interfaces, wrappers, and design patterns.
- Do not introduce additional architectural layers or dependencies without a clear need.
- Follow the existing project structure and coding conventions.
- Create new abstractions only when they solve a concrete problem or significantly improve maintainability.
- Prefer straightforward code over speculative extensibility.
- Avoid overengineering.
- Keep changes focused on the requested scope and avoid unrelated refactoring.
- Never sacrifice benchmark correctness, workload semantics, or performance for code simplicity.
