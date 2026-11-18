# Workflow State: fix-postgres-client

- Request: fix PostgreSQL client issues from the YCSB client review (agent-team task, postgres-developer)
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-17, consolidated team plan: drop GIN, auto-prepare, parameterized projection, update miss)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-18)

## Decisions
- 2026-11-17 (user): approved all recommendations: remove GIN index, MaxAutoPrepare/AutoPrepareMinUsages, field names as SQL parameters with a new PostgresNoSQLHelper. Pool settings unchanged. No commit/archive without approval.
- 2026-11-17 (orchestrator): branch `fix-db-clients` from dev 9a22590 (#45); file ownership per database folder.

## Stage Log
- 2026-11-17 Stage 1/2: Phase 1 analysis by postgres-developer (architect, read-only) confirmed all issues vs PostgreNoSQLDBClient.java and README schema (GIN unused by any query; no prepared statements; update ignores row count; || merge matches). Artifacts written from the approved plan.

- 2026-11-17 Stage 3: dotnet-developer implemented tasks 1-2 (edits only; own files only). Team Lead: full rebuild 0 errors, warnings unchanged (Mongo 1, Postgres 13, Redis 6 as on dev); offline tests 80/80 (71 + 5 MongoDbHelperTests + 4 PostgresNoSQLHelperTests); diff limited to owned files; tasks 3.1/3.2 done. CLAUDE.md: offline filter + per-database comparability note.
- 2026-11-17 Stage 4: code-reviewer: no Critical/High/Medium; Low notes recorded in the final report.
- 2026-11-17 Stage 5: validate --strict passes; live tests (3.3) not run (need approval).
- 2026-11-17 (user): "run live tests". Team Lead verified project containers (mongodb started from MongoDb compose after confirming 27017 free; postgres and scylla1-3 from this repo). Live results: MongoDbTests 10/10, PostgresNoSqlTests 18/18, ScyllaDbTests 15/15 (QUORUM bulk load: no timeouts).
- 2026-11-18 (user): removed `DROP INDEX IF EXISTS idx_ycsb_table_jsonb` from InitDb (IDE edit); confirmed option A in scope-delete-all-data: the reset (DROP+CREATE TABLE) removes the legacy index. Spec, design, proposal and task 1.2/2.2 wording updated; the live test InitDb_Drops_Legacy_Gin_Index_And_Keeps_Data is replaced in scope-delete-all-data.

- 2026-11-18 (user): "archive, commit and push". Archived in order fix-mongodb-client, fix-postgres-client, fix-scylladb-consistency, scope-delete-all-data; committed and pushed on branch fix-db-clients.

## Next Step
Done.
