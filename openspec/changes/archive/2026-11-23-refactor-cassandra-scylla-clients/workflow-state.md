# Workflow State: refactor-cassandra-scylla-clients

- Request: Refactor the Cassandra and ScyllaDB helpers and clients to reduce duplication and improve readability without changing YCSB behaviour: merge ExtractAllFields/ExtractFields; BuildInsertBindings as object?[] without null!; keep BuildUpdateBindings and the prepared-statement caches; one private read method for Read/ReadModifyWrite; BulkInsert via Parallel.ForEachAsync (512); apply equally to ScyllaDB; focused tests only. Review the code and propose the smallest changes; wait for approval before modifying files.
- Current stage: 7 Archive
- Status: done
- Plan approved: yes (2026-11-23, Cassandra-only scope; BulkInsert failure behaviour with Parallel.ForEachAsync approved; live CassandraTests via the lock script approved)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-23)

## Decisions
- 2026-11-23: skip_specs (no observable behaviour change in the specs) (lead).
- 2026-11-23: user limited the scope to CassandraHelper.cs and CassandraYcsbClient.cs. ScyllaDB is not modified; it will be refactored separately when its integration tests are available.
- 2026-11-23: user approved the BulkInsert failure behaviour of Parallel.ForEachAsync (no new inserts after the first failure).
- 2026-11-23: user approved build, offline tests and live CassandraTests through the existing lock script after implementation. No commit or push.

## Stage Log
- 2026-11-23 Stage 0: no active changes, so no duplicate.
- 2026-11-23 Stage 1: proposal, design and tasks created (skip_specs); validate --strict passes.
- 2026-11-23 Stage 2: the architect approved with edits, all applied:
  - fixed the `fieldList ?? _fieldNames` compile error (IReadOnlyList local);
  - single ToList; null-row check rather than Count;
  - tightened BulkInsert failure wording; (item, _) lambda;
  - no offline test is possible without a seam, so coverage comes from the live tests (Workload_* RecordCount 1000 > 512);
  - aligned helper comments too;
  - snapshot the untracked Cassandra originals before editing;
  - expected no helper test edits.
- 2026-11-23 Plan revised to Cassandra-only after user approval (proposal, design, tasks updated).
- 2026-11-23 Stage 3: dotnet-developer edited CassandraHelper.cs and CassandraYcsbClient.cs (CRLF kept). Build 0 errors, 29 warnings before and after (identical). Offline 147/147. Live CassandraTests 23/23 via live-lock.sh (down without -v, lock released, no stacks running). ScyllaDb unchanged. Originals and diff in the session scratchpad (cassandra-orig/, cassandra-refactor.diff).

- 2026-11-23 Stage 4: code-reviewer found no blocking issues. Nits: the copied cache comment says "prepare only once" but GetOrAdd may race a duplicate PrepareAsync (harmless; left as copied from Scylla); one extra async state machine per Read/RMW (accepted in design); CLAUDE.md "line-for-line copy" wording now stale.
- 2026-11-23 Stage 5: verify: all artifacts done, tasks 7/7, skip_specs (no deltas), validate --strict passes, implementation matches design per review.

## Next Step
Archived 2026-11-23 with user approval. Open follow-ups: CLAUDE.md "line-for-line copy" wording; optional cache-comment rewording; ScyllaDB refactor once its live tests can run.
