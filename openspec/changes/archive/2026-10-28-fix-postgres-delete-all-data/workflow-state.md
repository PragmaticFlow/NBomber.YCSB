# Workflow State: fix-postgres-delete-all-data

- Request: Fix PostgreSQL DeleteAllData implementation
- Current stage: 7 - Archive
- Status: done
- Plan approved: yes (2026-10-28, revisions R1-R9, error handling option B, implementation plan)
- Correction cycles used: 0/2
- Archive approved: yes (2026-10-28, archive and sync specs to openspec/specs/; no commit)

## Decisions
- 2026-10-27: Scope limited to PostgreSQL `DeleteAllData`; `YcsbScenario` unchanged (orchestrator proposal, approved by user 2026-10-28).
- 2026-10-28: User approved architect revisions R1-R9.
- 2026-10-28: User chose option B: log the cleanup error and rethrow instead of returning Fail.
- 2026-10-28: User approved running the PostgreSQL integration tests against the project's own local Docker container after verifying the connection target. Project is pre-deployment; no production data exists. Containers/databases of other projects must not be modified.

- 2026-10-28: User approved two optional test improvements (empty-table test, schema-scoped pg_indexes), limited to PostgreSQL tests; added as tasks 2.6 and 2.7; task 2.5 now runs the three DeleteAllData_* tests. User will start Docker Desktop; approved running the three tests after verifying the container belongs to NBomber.YCSB. No archive or commit without approval.

## Stage Log
- 2026-10-27 Stage 0: OpenSpec root found; no active changes, no duplicate.
- 2026-10-27 Stage 1: Created change with proposal.md, specs/postgres-client/spec.md, design.md, tasks.md. `openspec validate --strict` passes.
- 2026-10-27 Stage 2: Architect review done. Root cause confirmed (PostgresNoSQLYcsbClient.cs:317). Orchestrator verified only the Postgres client catches DeleteAllData errors.
- 2026-10-28 Stage 2: Applied R1-R9 and option B to all four artifacts. `openspec validate --strict` passes.

- 2026-10-28 Stage 3: dotnet-developer completed tasks 1.1-1.3, 2.1-2.4 (ticked). DeleteAllData now `TRUNCATE TABLE {TABLE_NAME}` + `throw;`. Build OK with `-p:NuGetAudit=false` (plain build blocked by pre-existing MessagePack NU1902). Offline tests 21/21 passed. Two new live tests added, not run.
- 2026-10-28 Task 2.5 blocked: Docker Desktop not running (no engine pipe, nothing on :5432). Orchestrator did not start Docker Desktop (would restart other projects' containers).
- 2026-10-28 Stage 4: code review done. No Critical/High; no correction cycle. Low: pg_indexes query not schema-scoped (tests:329-330); hard-coded index names (accepted). Missing optional test: "Reset on an empty table" scenario.
- 2026-10-28 Stage 5: openspec-verify-change run. 7/8 tasks; 1 CRITICAL = task 2.5 incomplete (blocked, Docker down). All 3 requirements implemented; design followed; `validate --strict` passes. WARNING: scenario "Reset on an empty table" has no test. SUGGESTION: schema-scope the pg_indexes query. Status set to blocked.

- 2026-10-28 Tasks 2.6/2.7: dotnet-developer added DeleteAllData_Succeeds_On_Empty_Table and schema-scoped the pg_indexes query. Build 0 errors/0 warnings (`-p:NuGetAudit=false`); offline 21/21.
- 2026-10-28 Task 2.5: Docker up. Verified `postgres` = compose project `posgresnosql` (repo PosgresNoSQL folder), :5432 served only by com.docker.backend + wslrelay -> Docker Desktop user-distro proxy (no Ubuntu postgres), DB mydb/myuser PG 18.1. Ran only the 3 DeleteAllData_* tests with exact filters: 3/3 passed. Final DB state: ycsb_table 0 rows, indexes idx_ycsb_table_jsonb + ycsb_table_pkey, only table in public schema. No other containers touched; no `down -v`.
- 2026-10-28 Stage 5 (re-run): 10/10 tasks, `validate --strict` passes, all 3 requirements implemented, design followed; no critical issues or warnings.
- 2026-10-28 Stage 6: final report delivered.

- 2026-10-28 Stage 7: user approved archive. Synced delta to new main spec openspec/specs/postgres-client/spec.md (3 requirements, 6 scenarios; `openspec validate --specs` and `--strict` pass). Archived to openspec/changes/archive/2026-10-28-fix-postgres-delete-all-data/. No commit.

## Next Step
None - change complete and archived. (Superseded notes below kept for history.)
Stages 4 and 5 done except task 2.5. Ask the user whether to address the empty-table test WARNING and the pg_indexes SUGGESTION (scope change; not automatic). Task 2.5 waits for the user to start Docker Desktop and the project's postgres container; then verify the :5432 target is compose project `posgresnosql` and run:
`dotnet test tests/NBomber.YCSB.Tests/NBomber.YCSB.Tests.csproj -p:NuGetAudit=false --filter "FullyQualifiedName=NBomber.YCSB.Tests.PostgresNoSqlTests.DeleteAllData_Removes_All_Rows_And_Keeps_Table_Usable|FullyQualifiedName=NBomber.YCSB.Tests.PostgresNoSqlTests.DeleteAllData_Then_BulkInsert_Stores_Fresh_Values"`
After the tests pass: tick 2.5 in tasks.md, re-run openspec-verify-change, give the Stage 6 report, and ask for archive approval (Stage 7; never automatic).
