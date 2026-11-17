# Workflow State: datagenerator-workload-decoupling

- Request: Refactor `DataGenerator` to completely remove its dependency on `WorkloadManager` and `OperationType` (YcsbScenario computes insertProportion and usesReadLatest once and passes them to the constructor; eager ζ; no lazy init; identical behaviour)
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-17, revised plan after architect review)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-17)

## Decisions
- 2026-11-17 (user): explicit architectural requirement: YcsbScenario owns workload/operation decisions; DataGenerator gets values via constructor. Do not archive.
- 2026-11-17 (orchestrator): work on the current branch `fix-exportfile` (its changes are uncommitted; a new branch would carry them anyway); the user can split commits/PRs later. skip_specs (no behaviour change).

## Stage Log
- 2026-11-17 Stage 0: active change fix-exportfile is a different request; no duplicate. Call sites: YcsbScenario (1), DataGeneratorTests (9 direct + CreateOrdered), ScanLengthTests (Draw). DataGenerator references WorkloadManager/OperationType only in GetZipfianItemCount and CreateLatestZipfian; it also calls settings.GetMaxScanLength(), which uses WorkloadManager inside YcsbCliArgs (out of scope).
- 2026-11-17 Stage 1: created proposal, design, tasks (skip_specs).
- 2026-11-17 Stage 2: architect: approve with minor edits. Confirmed: simplest design; required ctor params (a default insertProportion 0 would silently shrink the item count); unconditional guard equivalent for A-F and still before InitDb (field initialiser order guard -> overflow -> ζ kept); insertProportion bit-identical (0 / 0.05); no spec text affected, skip_specs right; no conflict with fix-exportfile. Applied: test helper passes insertProportion 0 also for read-latest tests (no copy of D's mix; D tests do not observe item count); named arguments in tests; stale comments (GetKeyZipf, _latest) updated; grep also checks settings.Workload.
- 2026-11-17 Stage 3: dotnet-developer: tasks 1.1-3.3 done (Edit tool only; CRLF/BOM preserved). Orchestrator: diff vs saved pre-refactor copies matches design (scratchpad decouple.diff); full rebuild 0 errors, 20 pre-existing warnings; offline 71/71; grep: no WorkloadManager/OperationType/settings.Workload in DataGenerator.cs. Before/after probe (HEAD worktree, old ctor vs new, removed afterwards): A-F at recordcount 1000, operationcount 100000, seed 42 - item counts (A,B,C,F 1001; D,E 11001), 3000 keys per workload with acks, recordcount-0 ArgumentException: identical.
- 2026-11-17 Stage 4: code-reviewer: no Critical/High. Medium: unconditional recordcount-0 guard would reject a future insert-only mix (documented in design Risks; revisit with T9). Low: missing space at DataGeneratorTests.cs:222; test name `..._For_Workload_D` now workload-free; no direct tests for insertProportion -> item count, usesReadLatest false -> InvalidOperationException; helper always passes insertProportion 0. Pre-existing: division by a zero weight sum (unreachable with presets). Reviewer's CLAUDE.md note is already resolved (CLAUDE.md says "all workloads (A-F)").
- 2026-11-17 Stage 5: verify: 6/7 tasks (4.1 optional live check); design followed; skip_specs (no behaviour change, confirmed by probe); validate --strict passes.

- 2026-11-17 (user): edited the code before commit: removed usesReadLatest; ζ built for every workload. Test build broke (11 errors); user asked to update the tests to their changes. dotnet-developer updated DataGeneratorTests/ScanLengthTests (Edit only). Orchestrator: full rebuild 0 errors; offline 71/71 (fresh build); before/after probe identical (A-F item counts, 18,000 keys, recordcount-0 exception). Artifacts updated to the two-argument design. User: "archive, commit and push". Task 4.1 not run.

## Next Step
Done. Archived 2026-11-17; committed and pushed with the other change on branch fix-exportfile.
