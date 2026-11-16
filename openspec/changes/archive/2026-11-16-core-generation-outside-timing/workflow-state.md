# Workflow State: core-generation-outside-timing

- Request: implement T2, create new branch for this task (T2 from the Core gap analysis: move key/value/field generation out of the timed step and replace Bogus with Random.Shared)
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-16, plan with architect edits A-E)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-16, user: "archive it and make commit with comment, push and create PR into dev")

## Decisions
- 2026-11-16 (user): implement T2; create a new branch. Created `core-generation-outside-timing` from dev (d42f533).
- 2026-11-16 (user, earlier in session): replace Bogus with `Random.Shared` (option B), keep lowercase values.
- 2026-11-16 (orchestrator): no Git commit; no live DB tests without approval.

## Stage Log
- 2026-11-16 Stage 0: OpenSpec root found; no active changes; no duplicate. Branch core-generation-outside-timing created from up-to-date dev.
- 2026-11-16 Stage 1: Created proposal.md, specs/core-workload/spec.md (modified capability, 5 ADDED requirements), design.md, tasks.md.

- 2026-11-16 Stage 1: `openspec validate --strict` passed.
- 2026-11-16 Stage 2: Architect verdict "ready after small edits"; claims verified (Step.Run measures the lambda; context.Random consumption order unchanged; RMW Task return safe; Bogus only in DataGenerator/csproj). Applied A (Bogus static Randomizer.Locker, per-thread seeds don't remove it; from Bogus source), B (only per-step stats exclude generation; scenario-level stats unchanged), C (parallel GetKeyNext distinct-keys test), D (GenerateRandoms runs in WithInit), E (test settings note). Noted unrelated: csproj `(WarningsAsErrors)` missing `$` - raise separately. Re-validated.

- 2026-11-16 (user): "approve" - plan approved.
- 2026-11-16 Stage 3: dotnet-developer completed tasks 1.1-3.2 (all ticked). Files: Infra/DataGenerator.cs, YcsbScenario.cs, NBomber.YCSB.csproj (Bogus removed), new tests/DataGeneratorTests.cs (9 tests). Build 0 errors, audit passes, 20 pre-existing warnings in untouched clients. Offline tests 51/51. No Bogus/Faker left. No staging, no commit, no DB tests.

- 2026-11-16 Stage 4: code review done. No Critical/High/Medium; no correction cycle. Lows informational only (BOM kept; fieldcount<=0 throws - T1 scope; closure allocation outside timed region). Missing tests accepted: "latency covers only DB call" verified by review; optional live Workload_* run not done (needs approval).
- 2026-11-16 Stage 5: verify. 6/6 tasks; 5/5 requirements implemented; 10/10 scenarios covered (8 by DataGeneratorTests, 2 timing/naming scenarios by code review); design followed; validate --strict passes. Orchestrator re-ran: --no-incremental build 0 errors, audit passes, 20 warnings only in untouched Mongo/Postgres/Redis clients; offline tests 51/51.
- 2026-11-16 Stage 6: final report delivered.
- 2026-11-16 (user): move the alphabet constant to the top of the class with a camelCase name. dotnet-developer: `private const string alphabet` is now the first member of DataGenerator; GenerateFieldValue uses it; design.md snippet updated. Build 0 errors (same 20 warnings), offline tests 51/51. Style-only; no re-review needed.
- 2026-11-16 (user): constants in upper case. Renamed to `ALPHABET` (DataGenerator.cs:9, used at :145); design.md updated. Build 0 errors, offline tests 51/51. Added to .claude/agents/dotnet-developer.md "Coding standards": class-level consts at the top of the class, UPPER_CASE names (user-requested).

- 2026-11-16 Stage 7: archive approved; synced 5 ADDED requirements into openspec/specs/core-workload/spec.md (now 9 requirements); change archived. User also asked to commit, push and open a PR into dev.

## Next Step
None. Change archived.
