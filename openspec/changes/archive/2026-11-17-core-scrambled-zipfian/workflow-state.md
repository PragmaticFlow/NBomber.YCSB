# Workflow State: core-scrambled-zipfian

- Request: implement T6 (Java-compatible scrambled zipfian key choice for read/update/scan/RMW)
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-16, revised plan with user adjustments 1-3)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-17)

## Decisions
- 2026-11-16 (user): implement T6 (reopens the Zipf decision set aside earlier).
- 2026-11-16 (orchestrator): branch `core-scrambled-zipfian` from dev 44a698f (includes #42). Read latest (D) unchanged; key names stay xxHash.
- 2026-11-16 (orchestrator): FNV-64 test values computed with a Node BigInt port of YCSB Utils.fnvhash64 (no JVM installed); simulation of Java scrambled zipfian at n=1000: hottest key ~3.9%, top-10 ~13% (current Zipf 1.3: ~28% / ~65%).

## Stage Log
- 2026-11-16 Stage 0: OpenSpec root found; no active changes; no duplicate.
- 2026-11-16 Stage 1: Read Java ScrambledZipfianGenerator, ZipfianGenerator, Utils.fnvhash64, CoreWorkload. Created proposal.md, specs/core-workload/spec.md (4 ADDED requirements), design.md, tasks.md.

- 2026-11-16 Stage 1: `openspec validate --strict` passed.
- 2026-11-16 Stage 2: Architect: approach right, revise before implementation. Formulas/FNV parity confirmed. Found parity bug: itemcount = recordcount + expectednewkeys + 1 (Java inclusive range, CoreWorkload L519 / ScrambledZipfian L87) - orchestrator verified with Node: hottest keys 145 (3.8%), 611 (2.0%), 214 (1.6%), top-10 ~12.6%. Applied A (+1 item count: 1001/1101), B (long key, redraw < 1; corrected design claim), C (limit 0 returns key 1; Math.Max(0) on expected new keys), D (keep existing RecordCount-2 test), E (exact hot keys 145/611 test + spec), F (E test via GetKeyNext+AcknowledgeInsert; max <= 1100; UPPER_CASE consts), G (ZETA_2_THETA Java form; precomputed rank-1 threshold), H (CLAUDE.md deviations wording). Re-validated.

- 2026-11-16 (user): before approving, asked the architect to verify 7 points against Java YCSB (item count, recordcount 0, deterministic tests, performance, read latest follow-up, hot keys vs Java, placement). Research only.
- 2026-11-16 Stage 2 (verification): Architect confirmed item count (A/B/C/F 1001, E 1101; D uses latest in YCSB and never builds it), 1-based mapping, precomputed ZETAN (no zeta recomputation), placement in DataGenerator. recordcount 0: YCSB maps to Integer.MAX_VALUE (meaningless for our in-memory load); recommended constructor guard for A/B/C/E/F instead of the key-1 fallback. Hot keys analytically: rank0 = 1/ZETAN 3.78%, rank1 1.90%, rank2 1.53%; fnv(0) re-derived by hand. Orchestrator re-checked with Node: u 0.01/0.04/0.065 -> ranks 0/1/2 -> keys 145/611/214; rc3 rank0 -> key 4 (redrawn), rank1 -> key 1; exact rank-mass sum: 145 3.82%, 611 1.96%, 214 1.56%. Applied revisions A-M: constructor guard (ArgumentException, A/B/C/E/F, D unchanged), no limit-0 branch, deterministic FixedRandom tests, no hot-key order assertion in the shape test, "at most one Math.Pow", item count wording (E, not D), read latest follow-up recorded. Re-validated.
- 2026-11-16 Defaults applied pending user decisions: guard in constructor; A/B/C/E/F only; ArgumentException; keep wide-tolerance shape test; FnvHash64 public static; no JVM check now; read-latest follow-up recorded in design/tasks (not a new change yet).

- 2026-11-16 (user): approved the revised plan with adjustments: (1) FnvHash64 private, test observable key behaviour; (2) item count safe for large recordcount/operationcount (checked arithmetic, ArgumentException; int*double exact below 2^53); (3) small-dataset tests (recordcount 1 and 2). Scope A/B/C/E/F; D unchanged (follow-up). Do not commit. Orchestrator computed with Node: item count 1000001 -> keys 801321/216075/971812 for ranks 0/1/2; rc1 itemcount 2: rank0 -> key 2 (redrawn), rank1 -> key 1; E int.Max/int.Max -> 2362232012. Artifacts updated; re-validated.

- 2026-11-16 Stage 3: dotnet-developer completed tasks 1.1-4.2. Files: Infra/DataGenerator.cs (+81), tests/DataGeneratorTests.cs (+173, 12 new test methods, CreateOrdered extended with workload/operationCount), CLAUDE.md. All Java-derived expected keys matched on first run. Build 0 errors (20 pre-existing warnings, none in DataGenerator). Offline tests 82/82 (DataGeneratorTests 40). Note: CLAUDE.md "no acknowledged-insert counter" deviation phrase is stale since T3 (left as is; out of scope).

- 2026-11-16 Stage 4: code review done. No Critical/High/Medium; no correction cycle. Low: recordcount 0 throws ArgumentException from YcsbScenario.Run as an unhandled exception (no clean CLI message; a validator rule belongs to T1); redraw loop terminates for all reachable states, slow only for tiny recordcount with huge operationcount on E (same as Java). Info: stale "no acknowledged-insert counter" phrase in CLAUDE.md (out of scope). Optional missing tests: negative OperationCount clamp, CLI error path. Orchestrator checked RavenDbTests: all RunWorkload calls use recordCount 1000.
- 2026-11-16 Stage 5: verify. 6/6 tasks; 7/7 requirements implemented; scenarios covered by DataGeneratorTests (FNV via key mapping at two item counts); design followed (FnvHash64/NextZipfianRank private; GetKeyLatest unchanged); validate --strict passes. Orchestrator re-ran: build 0 errors (20 pre-existing warnings); offline tests 82/82.
- 2026-11-16 Stage 6: final report delivered.

- 2026-11-16 (user): "move Zipf in new Class in folder Infra". Design updated: new Infra/ScrambledZipfianGenerator.cs (constants, ItemCount, NextKey, private NextRank/FnvHash64); DataGenerator keeps item count, guard and redraw. Task 1.3 added; tests unchanged.

- 2026-11-16 Stage 3 (rework): dotnet-developer created Infra/ScrambledZipfianGenerator.cs and slimmed DataGenerator (task 1.3 ticked). Build 0 errors; offline tests 82/82 unchanged. Orchestrator: no line-ending noise (DataGenerator 39+/1- vs HEAD); BOM matches other Infra files.
- 2026-11-16 Stage 4 (re-review): no Critical/High/Medium. Low (optional): ItemCount could be an auto-property; optional direct generator test. Behaviour-preserving extraction confirmed.

- 2026-11-16 (user): "roll back last changes" -> confirmed: only the class move. Orchestrator restored DataGenerator.cs to the reviewed pre-move version (saved diff applied to HEAD, 80+/1-), deleted Infra/ScrambledZipfianGenerator.cs, removed task 1.3 and the "Generator class" design section, restored proposal Code line. Build 0 errors; offline tests 82/82. Re-validated.

- 2026-11-16 (user): "remove unnecessary tests with Zipf, leave only critical and important". Orchestrator decision: keep mapping (RC 1000 cases), item count (A, E), large values + too large, redraw RC3, small datasets RC1/RC2 with unacknowledged insert, empty-dataset rejection (A, E), existing T3 test; remove 1,000,000 mapping cases, item count B/C/F cases, One_Record_Returns_Key_1, Allowed_For_Workload_D, shape test, E growth test. Spec scenarios they covered (skew shape, keys in range, new keys reachable in E, D allowed) remain verified by the earlier 82/82 run and code review.

- 2026-11-16 Trim done by dotnet-developer (test file only; SHAPE_SEED/GROWTH_SEED removed). Build 0 errors; offline tests 69/69 (DataGeneratorTests 27). Task 2.2 recorded in tasks.md.

- 2026-11-17 (user): "remove all tests in this task, not need to test Zipf in this app". Orchestrator restored tests/DataGeneratorTests.cs to dev (git checkout), removing all T6 tests; T3 test GetKeyZipf_Never_Returns_Unacknowledged_Key stays. Build 0 errors; offline tests 57/57 (same as before T6). tasks.md section 2, proposal tests line and design Tests section updated to record the decision.

- 2026-11-17 (user, via /opsx:archive with a question): asked for a one-line Zipf call like `random.Zipf(...)`; chose option 1: `Random` extension method `ScrambledZipf` in new Infra/ZipfianExtensions.cs. Not archived. Design and task 1.3 added.

- 2026-11-17 Stage 3 (rework): dotnet-developer created Infra/ZipfianExtensions.cs (`ScrambledZipf` extension), GetKeyZipf is a one-line call; task 1.3 ticked. Before/after 5 configs x 5,000 keys (Random 42): byte-identical (orchestrator confirmed with cmp, 25,005 lines each). Temp test deleted; no test changes. Build 0 errors; offline tests 57/57.
- 2026-11-17 Stage 4 (re-review): no Critical/High/Medium. Low: FnvHash64 comment slightly imprecise (long.MinValue maps to key <= 1, not always < 1; matches Java; ~2^-64); CLAUDE.md zipfian line could mention ZipfianExtensions; BOM confirmed by orchestrator (ef bb bf, same as DataGenerator).

- 2026-11-17 (user, via /opsx:archive with a review request): verify ZipfianExtensions against Java (statistical equivalence, exact values not required), then fix and add focused tests. Not archived. Found the user's in-progress edit (FnvHash64 returns the raw hash; the loop redraws long.MinValue and applies Math.Abs) left a stray `do { }` block: CS1002, build broken. Orchestrator removed the 5 stray lines. Constants re-checked with Node: ZETAN = zeta(10^10, 0.99) to 1e-10; eta 0.2122 (base 1 - eta > 0, no NaN); P(rank 0) 3.78%, P(rank 1) 1.90%. No other correctness issues. Added ZipfianExtensionsTests (8 cases). Build 0 errors (20 pre-existing warnings); offline tests 65/65.

## Next Step
Done. Archived 2026-11-17; 6 ADDED requirements synced to openspec/specs/core-workload/spec.md (23 requirements). Not committed.

## Backlog follow-up
- Read latest (workload D): Gray/ζ(n) vs NBomber Zipf; NBomber Zipf cost when n changes; `max <= 1 → key 1` fallback. See design.md "Follow-up".
