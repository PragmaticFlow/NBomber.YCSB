# Proposal

## Why

Workload D (95% read latest, 5% insert) chooses read keys with `DataGenerator.GetKeyLatest`, which calls NBomber's `random.Zipf(min(max, int.MaxValue), 0.99)` and returns `max - (rank - 1)`. A probe of NBomber 6.4.1 (scratch console, seeded `Random`) shows:

| n (acknowledged keys) | Cost per `Zipf` call |
|---|---|
| 1,000 | ~0.13 ms |
| 100,000 | ~10 ms |
| 1,000,000 | ~103 ms |

The cost is linear in n on **every** call, with no caching (same cost for a fixed n and for n growing by one). The distribution itself is exact (n = 100, 4,000,000 draws: every checked rank within 0.01 percentage points of `k^-0.99 / H(n, 0.99)`).

Key generation is outside the measured step (requirement "Operation latency covers only the database call"), so latency is not affected, but every read-latest operation spends this CPU time in the scenario copy. At YCSB-typical sizes (recordcount 10^6) a copy can do about 10 operations per second, so workload D throughput measures the key generator, not the database. At recordcount 1000 (live tests) the overhead is ~0.13 ms per operation, already comparable to a local Redis call.

Original YCSB uses `SkewedLatestGenerator`: `next = max - ZipfianGenerator.nextLong(max)`, with the Gray et al. draw (O(1) per call) and ζ(n, 0.99) kept up to date incrementally when n grows (O(new keys), `ZipfianGenerator.nextLong`). The same draw is already ported for T6 (`ZipfianExtensions`), with fixed n.

Other differences from YCSB found in the T6 follow-up:
- `max <= 1` returns key 1 even when no key exists (recordcount 0): the same kind of silent fallback that T6 replaced with a constructor error for A, B, C, E, F.
- n is capped at `int.MaxValue` (NBomber `Zipf` takes `int`).

A database-client fix is not possible: key choice is shared Core behaviour in `DataGenerator`.

## What Changes

- Read latest uses YCSB's skewed-latest algorithm: rank from the Gray et al. Zipfian draw with constant 0.99 over n = acknowledged key limit, key = n - rank (keys 1..n, the newest key is the most likely).
- ζ(n, 0.99) is computed once when `DataGenerator` is created in `YcsbScenario.Run`, before NBomber starts (once per run, shared by all copies), and extended incrementally when an insert acknowledgement advances the limit (under the existing acknowledgement lock, outside the measured step), as in YCSB. The draw itself is O(1) and lock-free.
- The Gray draw in `ZipfianExtensions` is shared by scrambled zipfian and read latest (no second copy of the formula). Scrambled zipfian behaviour is unchanged.
- recordcount 0 is rejected for workload D too (the existing guard for A, B, C, E, F is extended to read latest), so the `max <= 1 → key 1` fallback and the `int.MaxValue` cap are removed.
- Affected databases: **all**, through the shared Core, workload D only. No database client changes.
- **Out of scope**:
  - YCSB's Java quirk of computing eta with the item count from construction time (we use the current n; difference in the 4th decimal of eta).
  - YCSB's ζ over `recordcount − 1` items (0-based counter); we use ζ over keys 1..n (difference ~1/n).
  - `requestdistribution` overrides (T8), validation (T1), chunked load (T7).
  - Scrambled zipfian (A, B, C, E, F) behaviour.

## Capabilities

### New Capabilities
<!-- None. -->

### Modified Capabilities
- `core-workload`: adds requirements for the read-latest key distribution, its cost, and empty-dataset rejection for workload D. Modifies "Zipfian keys above the acknowledged limit are redrawn" only in its empty-dataset scenario (D is now included).

## Impact

- Code: `src/NBomber.YCSB/Infra/ZipfianExtensions.cs` (Gray draw for a given n and ζ(n)), `src/NBomber.YCSB/Infra/DataGenerator.cs` (`GetKeyLatest`, ζ(n) state, recordcount-0 guard). Tests: `tests/NBomber.YCSB.Tests/DataGeneratorTests.cs` and/or `ZipfianExtensionsTests.cs`.
- **Benchmark results change for workload D on every database**: throughput rises sharply at large recordcount (no longer limited by key generation); the distribution moves from exact Zipf(0.99) to YCSB's Gray approximation (the newest key keeps probability 1/ζ(n)). Earlier D results are not directly comparable, especially at recordcount ≥ 10^5. A, B, C, E, F are unchanged.
- `CLAUDE.md` Gotchas: the "read latest uses NBomber `Zipf(n, 0.99)`" deviation is removed; recordcount 0 is rejected for all workloads.
