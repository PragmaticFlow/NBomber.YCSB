# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

NBomber.YCSB is a .NET 9 console app that implements the Yahoo! Cloud Serving Benchmark (YCSB) on top of NBomber, with backends for Redis, MongoDB, PostgreSQL (JSONB), RavenDB and ScyllaDB. It is a benchmark tool: changes must keep results correct and comparable with the original Java YCSB and across databases. Project docs: https://nbomber.com/docs/load-testing-databases/overview

## Commands

```bash
dotnet build NBomber.YCSB.sln          # NuGet audit warnings NU1900-NU1904 are build errors

# Run a workload (one-shot CLI; the startup banner mentioning "interactive"/"exit" is stale)
dotnet run --project src/NBomber.YCSB -- run --workload A --db redis -p redis.host=localhost -p redis.port=6379

# Offline tests (no database needed)
dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"

# Single test
dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName=NBomber.YCSB.Tests.ScanLengthTests.Validator_Accepts_Defaults"

# Live tests for one database (what CI does per backend)
docker compose -f src/NBomber.YCSB/Redis/docker-compose.yml up -d --wait
dotnet test tests/NBomber.YCSB.Tests --filter "NBomber.YCSB.Tests.RedisTests"
```

Live test classes (`<Db>Tests`) need that folder's compose stack running on its default local ports. Their `Workload_*` tests run real workloads, which call `InitDb` + `DeleteAllData` first. Cleanup is destructive and differs per backend:
- Redis flushes the whole database.
- MongoDB drops every non-system database.
- RavenDB hard-deletes its database.
- Scylla drops and recreates `ycsb_table`.
- Postgres truncates `ycsb_table`.

Only run live tests against the project's own disposable containers, after checking nothing else (another project's container, a WSL service) is serving the port. Use exact `FullyQualifiedName=` filters when running individual live tests.

## Architecture

Flow: `Program.cs` parses the `run` command into `YcsbCliArgs`, validates it with `YcsbCliArgsValidator` (FluentValidation), picks a client in `GetYcsbClient` by `--db`, then calls `YcsbScenario.Run`.

- `YcsbScenario` builds one NBomber scenario. Init runs `InitDb`, then `DeleteAllData`, then `BulkInsert` of `recordcount` generated records. Each iteration picks an operation with `context.Random.Choice` over the workload's weighted operations and runs it as a named step. The load is `IterationsForConstant(threadcount, operationcount)`; `operationcount` is a total across copies, like YCSB. Step names (`read`, `update`, `insert`, `scan`, `read latest`, `read-modify-write`) are asserted by the live tests, so don't rename them.
- `src/NBomber.YCSB/Infra` holds the shared YCSB Core. Keep Core behaviour here, not in database clients:
  - `YcsbCliArgs`: typed options for Core settings (`--fieldcount`, `--minscanlength`, ...), plus `-p key=value` props that are passed to the database client.
  - `Workload.cs`: `WorkloadManager` per-workload presets (operation mix, defaults such as max scan length).
  - `DataGenerator`: keys (`user` + zero padding + number, hashed or ordered), field names and values, key choice, scan length.
  - `IDbYcsbClient`: the only database abstraction.
- One folder per backend (`Redis`, `MongoDb`, `PosgresNoSQL` (misspelled, keep it), `RavenDb`, `ScyllaDb`), each with a client implementing `IDbYcsbClient`, an optional static helper, and a `docker-compose.yml`. Clients read their connection settings from props via `YcsbCliArgs.TryGet`/`TryParseInt` using `<db>.<setting>` names. Scylla also receives `fieldcount` through props (`Program.cs`).
- Client contract: every operation returns NBomber `Response<object>` with `sizeBytes`, or `Response.Fail(statusCode: "no data")` on a miss. Clients never return record data. Update and ReadModifyWrite are partial updates and must preserve unrelated fields.

## Gotchas

- `MessagePack` in `NBomber.YCSB.csproj` is not used by our code. It pins NBomber's transitive dependency to a patched version; do not remove it, or the build fails the NuGet audit.
- `PostgresNoSQLYcsbClient` keeps its `NpgsqlDataSource` in a static field that each new instance replaces.
- Offline helper tests (`*HelperTests`, `ScanLengthTests`) are not run by CI, which only filters the per-database live test classes.
- Known deviations from Java YCSB are tracked as future OpenSpec changes: Zipf constant 1.3 instead of scrambled 0.99, no acknowledged-insert counter, xxHash instead of FNV, 1-based keys and `field1..N` names, the whole dataset built in memory, MongoDB/RavenDB/Scylla scan semantics.

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
