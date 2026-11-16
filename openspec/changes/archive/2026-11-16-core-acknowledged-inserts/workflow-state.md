# Workflow State: core-acknowledged-inserts

- Request: T3 create branch and implement this task (T3 from the Core gap analysis: acknowledged insert counter so reads never target in-flight inserts)
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-16, plan with architect edits A-G)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-16, user: "archive it, commit with comment and push")

## Decisions
- 2026-11-16 (user): implement T3; create a new branch. T2 was merged into dev as #39 (1645b53); created `core-acknowledged-inserts` from up-to-date dev.
- 2026-11-16 (orchestrator): no Git commit; no live DB tests without approval.

## Stage Log
- 2026-11-16 Stage 0: OpenSpec root found; no active changes; no duplicate. Branch core-acknowledged-inserts created from dev 1645b53.
- 2026-11-16 Stage 1: Read Java AcknowledgedCounterGenerator, CoreWorkload.nextKeynum/doTransactionInsert, SkewedLatestGenerator. Created proposal.md, specs/core-workload/spec.md (modified capability, 4 ADDED requirements), design.md, tasks.md.

- 2026-11-16 Stage 1: `openspec validate --strict` passed.
- 2026-11-16 Stage 2: Architect verdict "ready after small edits"; Java mapping, concurrency and signatures confirmed. Applied A (remove no-op SetRecordCount and its call), B (CreateValues before GetKeyNext; reserve immediately before try), C (finally required: NBomber can leave Step.Run by throwing after a failed step; no catch), D (pending bound qualified), E (GetKeyZipf test with RecordCount 2 so it fails without the fix), F (seeded Random, ulong.Parse, UPPER_CASE test consts), G (duplicate-ack test). Re-validated.

- 2026-11-16 (user): "approve" - plan approved.
- 2026-11-16 Stage 3: dotnet-developer completed tasks 1.1-4.2 (all ticked). Files: Infra/DataGenerator.cs, YcsbScenario.cs, tests/DataGeneratorTests.cs (6 new tests, LATEST_SEED/ZIPF_SEED consts). Build 0 errors (20 pre-existing warnings, none in touched src). Offline tests 57/57 (DataGeneratorTests 15). Test-strength checked by reasoning (Zipf(3,1.3) hits 3 ~26%/draw; latest rank 1 = reserved key). No staging/commit/DB tests.

- 2026-11-16 Stage 4: code review done. No Critical/High/Medium; no correction cycle. 3 Low (optional): local `const int count` in new parallel test not UPPER_CASE class-level (same pattern as existing test); Interlocked.Exchange inside lock is redundant but correct; failed/throw-ack path verified by inspection only. Reviewer independently confirmed the zipf (RecordCount 2) and latest tests would fail without the fix.
- 2026-11-16 Stage 5: verify. 8/8 tasks; 4/4 requirements implemented; 8/8 scenarios covered (7 by DataGeneratorTests, failed-insert ack by code review); design followed; validate --strict passes. Orchestrator re-ran: --no-incremental build 0 errors, 20 pre-existing warnings; offline tests 57/57; no SetRecordCount/IScenarioContext left in DataGenerator/YcsbScenario.
- 2026-11-16 Stage 6: final report delivered.
- 2026-11-16 (user): rename `_ackLimit` to `_lastAcknowledgedKey`. Done in DataGenerator.cs (lines 24, 40, 44, 48), design.md and tasks.md. Build 0 errors, offline tests 57/57. Rename only; no re-review needed.
- 2026-11-16 (user): `LastAcknowledgedKey` moved directly under `NextScanLength` (public one-line block); comment alignment fixed. Build 0 errors, offline tests 57/57. Added rule to .claude/agents/dotnet-developer.md: public properties and one-line public `=>` members in one block near the top, after fields, before other methods (user-requested). Layout only; no re-review needed.

- 2026-11-16 Stage 7: archive approved; synced 4 ADDED requirements into openspec/specs/core-workload/spec.md (now 13 requirements); change archived. User also asked to commit and push.

## Next Step
None. Change archived.
