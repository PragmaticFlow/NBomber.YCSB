# Tasks

## 1. Scrambled zipfian (Infra)

- [x] 1.1 In `src/NBomber.YCSB/Infra/DataGenerator.cs`, add the constants and `static readonly` values from design.md at the top of the class (after `ALPHABET`), the `_zipfianItemCount` field with `GetZipfianItemCount` (the `recordcount 0` guard for A, B, C, E, F, the `Math.Max(0, ...)` clamp, the `checked` sum with `ArgumentException` on overflow, and the `+ 1` comment), and `public long ZipfianItemCount => _zipfianItemCount;` in the public one-line block
- [x] 1.2 Replace the body of `GetKeyZipf(Random random)` with the version from design.md (limit read once, no limit-0 branch, `long` key, redraw while `< 1` or `> limit`) and add `NextZipfianRank` and `FnvHash64` as **private static** methods exactly as in design.md; do not change `GetKeyLatest`, key-name building or anything else
- [x] 1.3 Refactor (user decision, 2026-11-17, option 1): move the Zipfian constants, `NextZipfianRank` (as `NextRank`), `FnvHash64` and the redraw loop into a new `src/NBomber.YCSB/Infra/ZipfianExtensions.cs` with `public static ulong ScrambledZipf(this Random random, long itemCount, ulong maxKey)` as in design.md "Extension method"; `GetKeyZipf` becomes the one-line call; `GetZipfianItemCount`, `_zipfianItemCount` and `ZipfianItemCount` stay in `DataGenerator`; behaviour identical, verified by a one-time before/after key comparison (no tests added)

## 2. Tests (offline)

- `DataGeneratorTests.cs` is unchanged from dev (user decision, 2026-11-17); the existing T3 test `GetKeyZipf_Never_Returns_Unacknowledged_Key` stays.
- [x] 2.1 (user request, 2026-11-17 review) New `tests/NBomber.YCSB.Tests/ZipfianExtensionsTests.cs`, seeded `Random(42)`, 20,000 draws: keys in `[1, min(maxKey, itemCount)]` for 5 (itemCount, maxKey) cases; skew (hottest key 3-4.7%, top-10 10-15.5%); hot keys spread across the key space; argument guards

## 3. Documentation

- [x] 3.1 `CLAUDE.md` Gotchas: update the known-deviations line and add the note as in design.md "Documentation" (scrambled zipfian 0.99; read latest still NBomber `Zipf(n, 0.99)`; recordcount 0 rejected for zipfian workloads; earlier results not directly comparable)

## 4. Build and regression

- [x] 4.1 Run `dotnet build NBomber.YCSB.sln`; verify 0 errors and no new warnings in touched files
- [x] 4.2 Run the offline tests (`dotnet test tests/NBomber.YCSB.Tests --no-build --filter "FullyQualifiedName~DataGeneratorTests|FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"`); verify all pass. No live-database test is required: database clients are untouched

## Workflow follow-up

- Architect review and user approval before implementation (backend-task Stage 2).
- Separate follow-up (not this change): read latest (workload D) - Gray/ζ(n) vs NBomber `Zipf`, NBomber `Zipf` cost when n changes, `max <= 1 → key 1` fallback (see design.md).
- Optional, needs a JVM: `jshell` check of `site.ycsb.Utils.fnvhash64` and a 10^6-draw histogram of `new ScrambledZipfianGenerator(0, 1000)` (0-based hot keys 144, 610, 213).
- Optional, requires explicit approval: run one live workload test class against the project's local container.
- Archive only after explicit user approval.
