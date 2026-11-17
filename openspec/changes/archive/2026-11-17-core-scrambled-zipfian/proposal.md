# Proposal

## Why

Read, update, scan and read-modify-write keys (workloads A, B, C, E, F) are chosen by `DataGenerator.GetKeyZipf`, which calls NBomber's `random.Zipf((int)LastAcknowledgedKey, 1.3)`: an unscrambled Zipf distribution with exponent 1.3 over the current key range. Original YCSB uses `requestdistribution=zipfian`, implemented by `ScrambledZipfianGenerator` (`generator/ScrambledZipfianGenerator.java`):

- a Zipfian rank with constant **0.99** is drawn over a fixed universe of 10^10 items (`ITEM_COUNT`, precomputed `ZETAN = 26.46902820178302`) with the Gray et al. algorithm (`ZipfianGenerator.nextLong`);
- the rank is **scrambled** with `Utils.fnvhash64` and mapped onto the key range: `min + fnvhash64(rank) % itemcount`;
- the key range is `insertstart .. insertstart + insertcount + expectednewkeys`, with `expectednewkeys = (int)(operationcount * insertproportion * 2.0)` (`CoreWorkload.init`), and `nextKeynum` redraws any key above the acknowledged insert limit.

The difference is large. A simulation of both at 1000 records:

| | Hottest key | 10 hottest keys |
|---|---|---|
| Current: `Zipf(n, 1.3)`, unscrambled | ~28% of operations | ~65% |
| Java YCSB: scrambled zipfian 0.99 | ~3.9% (rank 0 alone: 1/ZETAN = 3.78%) | ~12.7% |

So our A/F runs are hot-row contention tests, C mostly measures a cache, and with `insertorder=ordered` the hot keys sit together at `user1..userK`, so E scans overlap heavily. Results are not comparable with published YCSB results. Comparisons between databases inside this tool use the same skew for all, but the skew is not YCSB's.

The user earlier set this question aside; this change reopens it as backlog task T6. A database-client fix is not possible: key choice is shared Core behaviour in `DataGenerator`.

## What Changes

- Zipfian key choice for read, update, scan and read-modify-write becomes YCSB's scrambled zipfian:
  - rank from a Zipfian distribution with constant 0.99 over 10^10 items (precomputed zeta, Gray et al. draw), using the scenario copy's `Random`;
  - scrambled with a port of YCSB's 64-bit FNV hash and mapped to keys `1 .. itemcount` (our keys are 1-based, YCSB's 0-based);
  - `itemcount = recordcount + expectednewkeys + 1` (YCSB's inclusive key range), `expectednewkeys = (int)(operationcount * insertproportion * 2)`, where insertproportion is the workload's insert share (0.05 for E, 0 for A, B, C, F; D uses read latest and, as in YCSB, does not use this item count);
  - a key above the acknowledged insert limit is redrawn, as in YCSB's `nextKeynum`; recordcount 0 is rejected when the generator is created for workloads A, B, C, E, F (clear exception before the database is initialised), because the redraw loop could never end. YCSB treats recordcount 0 as `Integer.MAX_VALUE`, which has no meaning with our in-memory load.
- Read latest (workload D) is unchanged: it already uses a Zipfian constant of 0.99 from the newest acknowledged key, like YCSB's `SkewedLatestGenerator`.
- Key names are unchanged (still xxHash for hashed insert order): the FNV hash here only scrambles ranks.
- No new tests (user decision): the behaviour was verified by temporary tests (all passed: exact rank-to-key mapping 145/611/214 and 801321/216075/971812, item count, large values, small datasets, redraw, recordcount-0 rejection, distribution shape) and code review, then the tests were removed at the user's request.
- Read latest differences (algorithm, cost when the key count changes, `max <= 1` fallback) are documented as a separate follow-up.
- Affected databases: **all**, through the shared Core. No database client changes.
- **Out of scope**:
  - Read latest generator (stays on NBomber `Zipf(n, 0.99)`).
  - `requestdistribution` overrides (uniform, hotspot, ...) - backlog T8.
  - Switching key names from xxHash to FNV.
  - Validation (T1), chunked loading (T7).

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `core-workload`: adds a requirement for YCSB scrambled zipfian key choice. Existing requirements (including "Reads target only acknowledged inserts") are unchanged.

## Impact

- Code: `src/NBomber.YCSB/Infra/DataGenerator.cs` (scrambled zipfian draw, FNV-64, item count, recordcount-0 guard), possibly `src/NBomber.YCSB/Infra/Workload.cs` (insert proportion from the existing operation mix). Tests: `tests/NBomber.YCSB.Tests/DataGeneratorTests.cs` (new tests; the existing `GetKeyZipf_Never_Returns_Unacknowledged_Key` T3 regression test stays unchanged).
- **Benchmark results change for every database in A, B, C, E, F**: much less skew, so fewer cache hits and less row contention; throughput and latency shift. Results before this change are not directly comparable; after it they follow YCSB's default distribution. D is unchanged.
- `CLAUDE.md` Gotchas lists "Zipf constant 1.3 instead of scrambled 0.99" as a known deviation; that line is updated.
