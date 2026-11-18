# Workflow State: scope-delete-all-data

- Request: fix DeleteAllData - in Postgres drop the table, in Scylla drop the table, in Mongo is it possible to drop the table not every non-system database?
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-18, revised plan: reset first, separate DeleteAllData/InitDb, RavenDB R2; plus comment cleanup)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-18)

## Decisions
- 2026-11-18 (orchestrator): not a duplicate of the open changes fix-mongodb-client / fix-postgres-client / fix-scylladb-consistency (different behaviour, same client files); stacks on them on branch fix-db-clients; archive after them (mongodb-client and scylladb-client main specs are created by those changes).
- 2026-11-18 (user, /backend-task): Scylla DeleteAllData should delete data, InitDb should create the table; no RecreateTable; scenario orchestrates. Answers: update this change (not a new one); scenario order DeleteAllData -> InitDb -> BulkInsert.
- 2026-11-18 (user): "approve and remove unnecessary and very understandable comments in branch" -> plan approved; task 1.7 comment cleanup in branch-changed .cs files.

## Stage Log
- 2026-11-18 Stage 0: 3 open changes (awaiting archive approval); DeleteAllData() has no table parameter; init order InitDb -> DeleteAllData -> BulkInsert; Scylla InitDb already drops/recreates the table.
- 2026-11-18 Stage 1: proposal, specs (postgres-client MODIFIED, mongodb-client ADDED, scylladb-client ADDED), design with options, tasks.
- 2026-11-18 Stage 2: architect approves with edits: Postgres DROP+CREATE in one command (no CASCADE), InitDb CREATE TABLE IF NOT EXISTS with shared definition, auto-prepare safe while DDL unchanged; Scylla S1 (shared RecreateTable; S2 rejected: stale columns when fieldcount grows); Mongo M1 (DropDatabaseAsync(databaseName)). High: Postgres InitDb lacks `DROP INDEX IF EXISTS idx_ycsb_table_jsonb` although fix-postgres-client 1.2/3.3 are ticked and its live test passed on 2026-11-17. Orchestrator: PostgresNoSQLYcsbClient.cs modified 2026-11-18 16:48, after the live run and by no agent -> likely the user's own edit; ask (decision 0.1). Artifacts updated.
- 2026-11-18 (user): "approve, A": plan approved; option A = no DROP INDEX in Postgres InitDb (user had already removed it); fix-postgres-client artifacts updated accordingly.

- 2026-11-18 Stage 3: three dotnet-developers (one folder each, Edit/Write only) implemented 1.1-1.3 and 2.1-2.3; Team Lead updated CLAUDE.md (3.1). Full rebuild 0 errors, no new warnings; offline tests 80/80; only owned files changed.
- 2026-11-18 Stage 4: code-reviewer: no Critical/High/Medium. Low: Scylla RecreateTable runs twice per run (init only, accepted S1); Postgres DROP needs ownership, InitDb alone no longer drops a legacy index (scenario always resets); new tests isolated; CLAUDE.md accurate. Optional tests: Postgres reset with table absent; Scylla reset after fieldcount change.
- 2026-11-18 Stage 5: validate --strict passes; 3.3 live tests pending approval.

- 2026-11-18 (user): "run live tests". MongoDbTests 11/11 and ScyllaDbTests 17/17 pass. Scylla first failed (UnavailableException at QUORUM) because after a Docker restart scylla2/3 started before the AIO sysctl and then swapped IPs; fixed non-destructively by restarting them in order (no volume changes). PostgresNoSqlTests NOT run: port 5432 is served by another project (nbombercloud-timescaledb-1, NBomber.Cloud); this project's postgres container is exited.

- 2026-11-18 Stage 1 (revision): proposal, design, tasks rewritten; specs: core-workload ADDED (init order), postgres-client MODIFIED (Reset benchmark data; Reset is limited), scylladb-client ADDED (separate reset and init), mongodb-client unchanged. RavenDB must tolerate reset before its database exists (R1/R2 for architect).

- 2026-11-18 Stage 2 (revision): architect approves with edits. Applied: core-workload requirement limited to order + fresh-database success (scope per client); Scylla guard = driver keyspace metadata check (catch rejected); RavenDB R2 (hard-delete moved to DeleteAllData, InitDb creates; R1 rejected: DeleteByQuery on the full old database can time out); new ravendb-client spec; tests: Postgres 4 tests call InitDb after reset, Scylla other-tables test InitDb in finally + rename + fresh-keyspace test, RavenDB fresh-database test; IDbYcsbClient doc comments; fix-postgres-client spec wording fixed (reset drops, init recreates).

- 2026-11-18 Stage 3 (revision): four dotnet-developers (Scylla, Postgres, Core+RavenDB, MongoDB comments) implemented 1.1-1.7, 2.1, 2.2, 2.4; Team Lead CLAUDE.md (3.1). Full rebuild 0 errors, no new warnings; offline 80/80; RecreateTable removed; only owned files changed.
- 2026-11-18 Stage 4: code-reviewer: no Critical/High. Medium M1: Scylla DeleteAllData keyspace guard reads the driver schema cache (can be stale if the keyspace is created/dropped by another session in the same process) -> proposed fix: no guard, catch InvalidQueryException around DROP (or query system_schema.keyspaces). Low: L1 two useful Scylla invariant comments removed (ExecuteUpdate key order, Scan field list); L3 Postgres other-tables test should InitDb in finally; L5 formatting (missing spaces); L2/L4/L6 notes.
- 2026-11-18 Stage 5: validate --strict passes; live tests pending.
- 2026-11-18 (user): stopped nbombercloud-timescaledb-1 themselves. Team Lead: ports 5432/8080 free; started this project's postgres and ravendb compose stacks. Live (current code, before M1/L fixes): PostgresNoSqlTests 19/19, MongoDbTests 11/11, ScyllaDbTests 18/18, RavenDbTests 7/7. Redis not run (no container; Redis client unchanged, order-independent).
- 2026-11-18 (user): "we need not use cluster now" -> Scylla keyspace metadata guard removed. Team Lead verified on Scylla 2026.1.5: DROP TABLE IF EXISTS on a missing keyspace succeeds (cqlsh exit 0; without IF EXISTS it errors). DeleteAllData = DROP TABLE IF EXISTS only; design/proposal updated. Resolves review M1. Build 0 errors; offline 80/80; ScyllaDbTests 18/18 incl. fresh-keyspace test.
- 2026-11-18 (user): deleted self-evident comments (Scylla x2, Postgres x1, RavenDB x2); kept Scylla replication and warm-up notes. User: "fix L3 and L5" -> Postgres other-tables test InitDb in finally; formatting (=CreateFields, /// </summary>). M1 resolved by guard removal; L1 skipped (user: no restating comments). Build 0 errors; offline 80/80; PostgresNoSqlTests 19/19.

- 2026-11-18 (user): "archive, commit and push". Archived in order fix-mongodb-client, fix-postgres-client, fix-scylladb-consistency, scope-delete-all-data; committed and pushed on branch fix-db-clients.

## Next Step
Done.
