# Design

## Context

`GetKeyLatest` (workload D) calls NBomber `Random.Zipf(n, 0.99)`, which costs O(n) per call (probe: 0.13 ms at n = 1000, 103 ms at n = 10^6). YCSB's `SkewedLatestGenerator` uses `ZipfianGenerator.nextLong(max)`: Gray et al. draw in O(1), with ζ(n) extended incrementally (`zeta(countforzeta, itemcount, theta, zetan)`) under `synchronized` when the item count grows. T6 already ported the Gray draw in `ZipfianExtensions.NextRank` with fixed n = 10^10 + 1 and precomputed `ZETAN`.

`DataGenerator` is created once per run in `YcsbScenario.Run`, before `Scenario.Create` and before NBomber starts, and is shared by all copies. The read-latest key is chosen before `Step.Run("read latest")`; `AcknowledgeInsert` runs in the `finally` after the measured insert step. Neither is measured.

## Goals / Non-Goals

- Goals: YCSB skewed-latest distribution; O(1) lock-free draw; no silent fallback for an empty dataset; reuse the existing Gray draw; θ = 0.99 in one place (`ZipfianExtensions`).
- Non-goals:
  - YCSB's quirk of recomputing eta with the constructor's `items` instead of the current item count (we use the current n).
  - YCSB computes ζ over `recordcount − 1` items (0-based `lastValue()`), so its oldest key is effectively unreachable; we use ζ(n) over keys 1..n (difference ~1/n in ζ: newest key 12.94% vs 12.95% at n = 1000).
  - Configurable theta; changing scrambled zipfian.

## Decisions

### Gray draw for any n (ZipfianExtensions)
- Generalise the private `NextRank` to `NextRank(Random random, long itemCount, double zetaN, double eta)`. `ScrambledZipf` passes `ZIPFIAN_ITEMS`, `ZETAN`, `ZIPFIAN_ETA`: same expressions on the same values, so its results are bitwise identical and its cost unchanged. `ZIPFIAN_RANK_1_LIMIT` and `ZIPFIAN_ALPHA` stay shared (they depend only on θ).
- New public members:

```csharp
// rank in [0, itemCount - 1]; the Gray formula can round to itemCount when u is within ~2^-53 of 1
public static long Zipfian(this Random random, long itemCount, double zetaN, double eta)
    => Math.Min(NextRank(random, itemCount, zetaN, eta), itemCount - 1);

public static double ZipfianEta(long itemCount, double zetaN)
    => (1 - Math.Pow(2.0 / itemCount, 1 - ZIPFIAN_CONSTANT)) / (1 - ZIPFIAN_ZETA_2_THETA / zetaN);

// zetaFrom + sum of 1 / k^θ for k = fromCount + 1 .. toCount (Java zeta(st, n, theta, initialsum))
public static double ZipfianZeta(long fromCount, long toCount, double zetaFrom)
```

- ζ terms are summed as `1 / Math.Pow(k, ZIPFIAN_CONSTANT)` in ascending k (Java `zeta()` form), so ζ(2) equals `ZIPFIAN_ZETA_2_THETA` bitwise, and an incrementally extended ζ equals a ζ computed in one pass.
- Edge cases: n = 1 → `uz = u < 1` → rank 0 (eta finite, unused). n = 2 → eta is NaN (0/0) but unused, because `uz = u·ζ(2) < ζ(2) = RANK_1_LIMIT`; .NET 9 converts NaN to 0 when casting to `long`, so even a 1-ulp difference stays in range. The `Math.Min` clamp keeps rank ≤ n − 1; rank ≥ 0 always holds.

### ζ(n) state in DataGenerator
- Snapshot `private sealed record LatestZipfian(long ItemCount, double ZetaN, double Eta)` in field `_latest`. (n, ζ, eta) must be read together, so they live in one immutable object read with `Volatile.Read` and written with `Volatile.Write`.
- Created in the constructor only for workloads containing `ReadLatest` (workload D): ζ(recordcount) via `ZipfianExtensions.ZipfianZeta(0, recordcount, 0)` (~20 ms at 10^6, once per run, before NBomber starts). Other workloads pay nothing and keep `_latest` null.
- `AcknowledgeInsert`, after `Interlocked.Exchange(ref _lastAcknowledgedKey, limit)` and still under the existing `_ackLock`: if `_latest != null && limit > _latest.ItemCount`, extend ζ with `ZipfianZeta(_latest.ItemCount, limit, _latest.ZetaN)`, compute eta, and `Volatile.Write` the new snapshot. No new lock. Publishing after the exchange keeps `snapshot.ItemCount <= LastAcknowledgedKey`; snapshots only grow because they are written only under `_ackLock`. Cost: one `Math.Pow` per newly acknowledged key, outside the measured step.
- `GetKeyLatest(Random random)`: `var s = Volatile.Read(ref _latest)`; `key = s.ItemCount - random.Zipfian(s.ItemCount, s.ZetaN, s.Eta)`. No lock, no branch, does not read `LastAcknowledgedKey`. If `_latest` is null (workload without read latest), throw `InvalidOperationException` naming the workload.
- Removed: the `theta` parameter, the `max <= 1` branch, the `int.MaxValue` cap, and `using NBomber.CSharp;` (only used for `Zipf`).
- Hot-path cost per draw: one volatile read, one `NextDouble`, at most one `Math.Pow`; no allocation.

### recordcount 0
`GetZipfianItemCount` already rejects recordcount 0 for workloads with Read/Update/Scan/ReadModifyWrite. Add `ReadLatest`, so every workload (A-F) rejects recordcount 0 with the existing `ArgumentException` naming recordcount, before any database operation. No existing test or live test uses recordcount 0.

### IDbYcsbClient
Unchanged. Step names unchanged (`read latest`).

### Destructive operations
None. Tests are offline.

## Risks / Trade-offs

- [D results change] → Intended: throughput no longer limited by key generation; distribution is YCSB's Gray approximation (at n = 1000 the 10 newest keys get ~39.9% vs ~38.3% for exact Zipf; the newest key is exact 1/ζ(n) in both). Documented; earlier D results are not directly comparable.
- [ζ init time for very large recordcount] → O(recordcount) `Math.Pow` once (10^8 ≈ 2 s), same as YCSB; the in-memory dataset build is far larger.
- [Floating-point accumulation in ζ] → Summation in double, ascending k, same as YCSB.
- [Concurrency] → (n, ζ, eta) are published together in one immutable snapshot under the existing `_ackLock`; draws are lock-free. A read may briefly see the previous snapshot after an acknowledgement; it still only chooses acknowledged keys.

## Migration Plan

No migration. Rollback is reverting the change.
