# Workflow State: add-scan-length-config

- Request: Implement only `minscanlength` and `maxscanlength` in `YcsbCliArgs`; scanlengthdistribution fixed to uniform; defaults 1..100 for E, 1..1000 otherwise; explicit values override; validate 1 <= min <= max; shared Core in Infra only; research phase, no code yet.
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-10-28, edits A-F and implementation plan; typed options, no -p aliases)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-13, user: "archive it")

## Decisions
- 2026-10-28 (user): scanlengthdistribution fixed to uniform; no zipfian scan lengths.
- 2026-10-28 (user): database-specific scan issues handled separately.
- 2026-10-28 (user): research phase; no application code changes until the plan is approved.
- 2026-10-28 (orchestrator assumption, to confirm at approval): typed options `--minscanlength` / `--maxscanlength` like `--fieldcount`; no Java-style `-p` aliases.

- 2026-10-28 (user): approved edits A-F and the plan; confirmed typed options `--minscanlength`/`--maxscanlength` without `-p` aliases. Extra requirements: simplest readable code, reuse existing classes, Core in Infra, explicit values override defaults, `NextInt64` inclusive bounds safe at int.MaxValue, deterministic statistical tests with reasonable tolerance, no database client or unrelated changes, run offline tests and build.

## Stage Log
- 2026-10-28 Stage 2: Applied edits A-F; `openspec validate --strict` passes.
- 2026-10-28 Stage 3: dotnet-developer completed tasks 1.1-3.2 (all ticked). Files: Infra/Workload.cs, Infra/YcsbCliArgs.cs, Infra/YcsbCliArgsValidator.cs, Infra/DataGenerator.cs, YcsbScenario.cs (+46/-2), new tests/ScanLengthTests.cs (21 tests). Plain `dotnet build` 0 errors (NuGet audit passes without flags). Offline tests 42/42 (ScanLengthTests 21/21). No DB tests. Earlier Redis/Scylla/Postgres work was committed by the user (532444f, 315f417).
- 2026-10-28 Stage 4: code review done. No Critical/High; no correction cycle. 5 Low (test-strength): no parse->validate test, no CLI-level min-only parse test, uniformity is mean-only, int.MaxValue test checks range only, no max=0 validator test.
- 2026-10-28 Stage 5: openspec-verify-change. 9/9 tasks; 4/4 requirements implemented; 9/9 scenarios covered by tests; design followed exactly; `validate --strict` passes. Orchestrator re-ran: plain build 0 errors/0 warnings (no audit flag), offline tests 42/42, ScanLengthTests 21/21. No critical issues or warnings; 2 suggestions.
- 2026-10-28 Stage 6: final report delivered.
- 2026-11-13: user committed and pushed the implementation (72d9ad6 on test-dotnet-subagent).
- 2026-11-13 Stage 7: archive approved; synced delta to new main spec openspec/specs/core-workload/spec.md (4 requirements) and archived the change.

## Next Step
Stage 7: ask the user for explicit archive approval (openspec-archive-change, creates openspec/specs/core-workload/spec.md). Do not commit. (Superseded:)
- 2026-10-28 Stage 0: OpenSpec root found; no active changes; no duplicate.
- 2026-10-28 Stage 1: Created proposal.md, specs/core-workload/spec.md (new capability), design.md, tasks.md. `openspec validate --strict` passes.

- 2026-10-28 Stage 2: Architect review done. Ready after small edits A-F (no design change): A typed-options rationale + `-p` ignored note; B option descriptions; C idiomatic FluentValidation rules; D min-only + int.MaxValue range tests; E parse test for Option<int?>; F live E tests keep passing but slower. Architect's "remove ## Purpose" note rejected: OpenSpec requires Purpose for new-capability deltas. Awaiting user confirmation of edits and plan approval.

## Next Step
On user confirmation: apply edits A-F, run `openspec validate --strict`, record approval, start Stage 3. (Earlier note:) Architect reviews the artifacts (Stage 2). Apply recommended artifact edits via openspec-update-change after user confirmation, re-validate, present the plan, and wait for approval. Do not start Stage 3 without recorded approval.
