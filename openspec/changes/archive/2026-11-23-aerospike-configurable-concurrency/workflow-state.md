# Workflow State: aerospike-configurable-concurrency

- Request: Aerospike client configuration and policy improvements: make MAX_CONCURRENCY configurable via `aerospike.maxconcurrency` (default 512, > 0) for asyncMaxCommands, asyncMaxConnsPerNode and BulkInsert parallelism; keep all RecordExistsAction, timeout, retry and scan behaviour; update README/CLAUDE.md; focused tests.
- Current stage: 7 Archive
- Status: done
- Plan approved: yes (2026-11-20, plan with the user adjustment: default 512 only when absent; any explicit non-positive-integer value fails fast naming the setting. Live AerospikeTests via the lock script approved.)
- Correction cycles used: 1/2
- Archive approved: yes (2026-11-23, conditional on the overflow test, offline tests and the review passing)

## Decisions
- 2026-11-20: New capability spec `aerospike-client` (no existing Aerospike spec). Parsing goes in `AerospikeHelper.GetMaxConcurrency` for offline tests (lead, pending architect review).
- 2026-11-20: Existing live AerospikeTests cover partial update, missing-key update, duplicate insert and BulkInsert replace; no new live tests (lead).

## Stage Log
- 2026-11-20 Stage 0: no active changes, so no duplicate.
- 2026-11-20 Stage 1: proposal, spec (aerospike-client), design and tasks created; validate --strict passes.
- 2026-11-20 Stage 2: the architect approved with edits. The helper is justified (offline default/custom tests; TiDbHelper.GetDatabase precedent). Invalid values are tested via the constructor (it throws before connecting) with an ex.Message assert. Spec header and wording fixed, and the queue-wait scenario softened (whether the queue wait counts toward totalTimeout is unverified). Doc wording: "not comparable with stacks that persist writes to disk before acknowledging"; report non-default values. Existing live tests cover items 4-7. The Java CREATE_ONLY/REPLACE_ONLY claims were verified by the lead from the Java source. Validate --strict passes.

- 2026-11-20: User approved, with an adjustment. Explicit invalid values (incl. non-numeric/empty) fail fast with ArgumentException naming the setting; 512 only when absent. Spec, design and tasks updated.

- 2026-11-20 Stage 3: implemented. AerospikeHelper.GetMaxConcurrency added; the client uses _maxConcurrency (validated before the try); 8 offline tests; README/CLAUDE.md per tasks 2.1/2.2. Build 0 errors, 29 warnings (same as before). Offline suite 146/146 (the lead re-ran it; the developer's 125 used an older filter). AerospikeHelperTests 14/14. Live AerospikeTests 25/25 via live-lock.sh on a fresh container. All tasks checked.

- 2026-11-20 Stage 4: no Critical/High. Medium: private TryUpdate was renamed to Update (3 places, AerospikeYcsbClient.cs:72,92,144); out of scope, no behaviour change, file modified 21:10:21 during the developer's run (developer did not report it). Low: no overflow ("99999999999") test case; the raw value in the message is not asserted. No auto-fix (Medium/Low).
- 2026-11-20 Stage 5: validate --strict passes; tasks 8/8; spec scenarios covered by offline tests (default, custom, invalid incl. constructor) and the existing live tests (partial update, missing key, duplicate insert, bulk replace, more than 512 concurrent operations, scan/E).
- 2026-11-20 Stage 6: final report given.

- 2026-11-23: User: the TryUpdate -> Update rename is their own edit; keep it. Review finding 1 is resolved (no change).

- 2026-11-23: User approved the overflow test ("99999999999"; the exception must contain the property name and the invalid value; test-only unless it reveals an issue). After the test, offline tests and a short review pass; archive is approved if everything passes. No commit or push.

- 2026-11-23 Cycle 1: overflow InlineData("99999999999") plus `Assert.Contains(value, ex.Message)` added; no production change (int.TryParse rejects the overflow). AerospikeHelperTests 15/15; offline suite 147/147. Short review: approved, Low only (the "" case is trivially true; no Int32 boundary test). The archive conditions are met.
- 2026-11-23 Stage 7: archived (no commit).

## Next Step
None. The change is archived.
