# Workflow State: core-read-latest-generator

- Request: investigate Workload D task (read-latest follow-up from core-scrambled-zipfian)
- Current stage: 7 - Archive (done)
- Status: archived
- Plan approved: yes (2026-11-17, revised plan after architect review; user confirmed: single immutable snapshot with Volatile read/write, zeta extended exactly once per newly acknowledged key incl. batches, Gray draw adapted per n)
- Correction cycles used: 0/2
- Archive approved: yes (2026-11-17)

## Decisions
- 2026-11-17 (orchestrator): branch `core-read-latest` from dev f6d71e9 (includes T6 #43).

## Stage Log
- 2026-11-17 Stage 0: OpenSpec root found; no active changes; no duplicate.
- 2026-11-17 Stage 1: probe of NBomber 6.4.1 `Random.Zipf` (scratch console outside the repo): exact distribution (n=100, 4M draws, within 0.01 pp) but O(n) per call with no caching: 0.13 ms at n=1000, 10 ms at 10^5, 103 ms at 10^6, same for fixed and growing n. Node: ζ(1000, 0.99) = 7.728953 (newest key 12.94%, 2nd 6.51%); Gray vs exact top-10 at n=1000: 39.85% vs 38.25%. Created proposal, delta spec (2 ADDED, 1 MODIFIED), design, tasks.

- 2026-11-17 Stage 2: architect: approve with changes. Applied: H1 ζ extended in AcknowledgeInsert under existing _ackLock, lock-free GetKeyLatest reading one immutable (n, ζ, eta) snapshot (no new lock; matches "Concurrent data generation"); H2 existing GetKeyLatest test set to Workload D (snapshot only for read-latest workloads; InvalidOperationException otherwise); M1 n=2 eta is NaN (unused), ζ summed as 1/Math.Pow(k, θ) ascending; M2 ZipfianZeta in ZipfianExtensions (θ in one place); M3 ζ over n vs Java recordcount−1 documented as non-goal; M4 "during scenario init" corrected (DataGenerator built once in YcsbScenario.Run before NBomber starts); L1 spec cost wording; L4 remove `using NBomber.CSharp;`; L5 both CLAUDE.md sentences. Tests trimmed by orchestrator to shape, growth, incremental-equals-full, small datasets, recordcount 0 (architect's parallel test and scrambled golden test left out: scrambled is bitwise unchanged and existing ZipfianExtensionsTests cover it).
- 2026-11-17 (user): before implementation asked to confirm (1) single immutable snapshot with Volatile read/write, (2) ζ extended exactly once per newly acknowledged key incl. batches, (3) Gray draw adapted to growing n; covered by the design -> plan approved.
- 2026-11-17 Stage 3: dotnet-developer: tasks 1.1-4.2 done. ZipfianExtensions (NextRank generalised; Zipfian, ZipfianEta, ZipfianZeta), DataGenerator (LatestZipfian snapshot, ζ extended in AcknowledgeInsert, lock-free GetKeyLatest, ReadLatest in recordcount-0 guard, `using NBomber.CSharp` removed), DataGeneratorTests (+6 tests, CreateOrdered workload param, existing test on D), CLAUDE.md. Orchestrator: full rebuild 0 errors, no warnings in touched files (20 pre-existing); offline tests 71/71; BOM/CRLF preserved.
- 2026-11-17 Orchestrator probe (scratch, outside repo): GetKeyLatest 151 ns/call at recordcount 10^6 (was ~103 ms), init 35 ms; batch out-of-order acks (1003, 1002, then 1001 + duplicate) give the same keys as recordcount 1003; 10,000 parallel ack+draw: 0 unacknowledged keys, final keys equal recordcount 11000; ScrambledZipf 80,000 keys (4 configs, seed 42) identical to dev (temporary git worktree, removed).
- 2026-11-17 Stage 4: code-reviewer: no Critical/High/Medium; all three user conditions verified; ScrambledZipf bitwise unchanged by reading. Low: n=2 1-ulp NaN path (rank 0 instead of 1, ~1e-16, no change); O(n) ζ init at very large recordcount (documented in design Risks); recordcount 0 for D now rejected (documented); nested record placement. Suggested missing tests: ScrambledZipf golden, batch/out-of-order ack, non-D InvalidOperationException, direct ZipfianZeta/Zipfian tests, concurrency smoke. Not added (scope); the first two verified by the orchestrator probe.
- 2026-11-17 Stage 5: verify: 8/9 tasks (5.1 optional live test, needs approval); spec requirements implemented (distribution, cost, MODIFIED empty-dataset guard incl. D); design followed; validate --strict passes.

## Next Step
Done. Archived 2026-11-17.

- 2026-11-17 (user): "investigate all tests and apply the backend developer rules (consts at top, UPPER_CASE)". dotnet-developer: DataGeneratorTests (Draws -> DRAWS; CreateOrdered, KeyNumber, CountLatest moved to the top), ScanLengthTests (Draws -> DRAWS), ScyllaDbTests (FieldCount -> FIELD_COUNT and RawSession -> RAW_SESSION moved to the top). Developer reported 71/71 offline. Incident: a developer PowerShell function named `Rd` (alias of Remove-Item) deleted DataGeneratorTests.cs and emptied ScanLengthTests.cs; both rewritten. Orchestrator verified DataGeneratorTests against the pre-incident version rebuilt from the saved diff: same lines (after rename), 25/25 member bodies identical, only order changed. Also found: the two-line "+ 1: YCSB builds ScrambledZipfianGenerator..." comment in DataGenerator.GetZipfianItemCount was removed at 18:34 (not part of the request; reported to the user, left as is).
- 2026-11-17 (user): "archive, commit and push". Archive approved. Task 5.1 (optional live D test) not run. Spec synced: MODIFIED "Zipfian keys above the acknowledged limit are redrawn" (D added to empty-dataset rejection), ADDED "Read latest key distribution" and "Read latest key choice cost" (core-workload now 25 requirements).
