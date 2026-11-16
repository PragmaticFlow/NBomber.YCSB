# Workflow State: core-warmup-option

- Request: T5 option B: `--warmup` in whole seconds, default 0; `WithoutWarmUp()` when 0; `--warmup 3` keeps the old 3-second behaviour; document that previous results used a 3-second warm-up and may not be directly comparable; do not change database-specific InitDb warm-up; verify first with an offline fake-client test whether warm-up operations are additional to operationcount and whether they modify database state; minimal implementation, reuse existing classes.
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-16, plan with architect edits A-F, without WarmUpTests)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-16, user: "archive it, commit and push")

## Decisions
- 2026-11-16 (user): option B with decisions 1-5 above.
- 2026-11-16 (orchestrator): branch `core-warmup-option` created from dev 8fad61d (includes #41).

## Stage Log
- 2026-11-16 Stage 0: OpenSpec root found; no active changes; no duplicate.
- 2026-11-16 Pre-plan spike (user-requested): dotnet-developer ran a temporary fake-client test against current code (3 s warm-up), then deleted it (git clean). Results: A 1 thread: fake 2000 ops vs measured 1000; D 1 thread: fake 2000 (101 inserts) vs measured 1000 (43 inserts) -> 58 warm-up inserts; A 4 threads ~5 ms/op: fake 1672 vs measured 1000 (time-capped). Init/Delete/Bulk once each. Warm-up is additional, changes data, excluded from NodeStats.
- 2026-11-16 Stage 1: Created proposal.md, specs/core-workload/spec.md (4 ADDED requirements), design.md, tasks.md.

- 2026-11-16 Stage 1: `openspec validate --strict` passed.
- 2026-11-16 Stage 2: Architect verdict "approve with minor edits"; NBomber API verified (ScenarioProps extension methods, default 30 s warm-up, order irrelevant). Applied A (testable D warm-up scenario + 4th test with 1000 ops and distinct keys), B (200 < calls <= 400 bound; Init/Delete/Bulk once), C (definitions of fake operation calls and measured count), D (CLAUDE.md plan aligned: Architecture bullet, Gotchas, offline filter), E (README `## Warm-up` before `## Links`, no usage section), F (API note in design Context). Re-validated.

- 2026-11-16 (user): "approve but without creating WarmUpTests". Removed the test task; design keeps the test list as reference only; CLAUDE.md filter adds only DataGeneratorTests. Re-validated.

- 2026-11-16 Stage 3: dotnet-developer completed tasks 1.1-5.2 (all ticked). Files: Infra/YcsbCliArgs.cs, Infra/YcsbCliArgsValidator.cs, YcsbScenario.cs, README.md, CLAUDE.md (26+/4-). Build 0 errors (20 pre-existing warnings); offline tests 57/57; `run --help` shows --warmup [default: 0]. No test file, no staging/commit, no workload run.

- 2026-11-16 Stage 4: code review done. No Critical/High/Medium; no correction cycle. 2 Low (docs, optional): DataGeneratorTests filter addition is a doc catch-up (mention in PR); "Offline helper tests" Gotcha does not list DataGeneratorTests. Informational: default-0/positive selection and validator rule have no automated test (user decision; covered by spike + review).
- 2026-11-16 Stage 5: verify. 7/7 tasks; 4/4 requirements implemented; scenarios covered by the pre-plan spike and code review (no new tests by user decision); design followed; validate --strict passes. Orchestrator re-ran: build 0 errors (20 pre-existing warnings); offline tests 57/57; YcsbScenario.cs:102-104 selection confirmed.
- 2026-11-16 Stage 6: final report delivered.

- 2026-11-16 Stage 7: archive approved; synced 4 ADDED requirements into openspec/specs/core-workload/spec.md (now 17 requirements); change archived. User also asked to commit and push.

## Next Step
None. Change archived.
