# Tasks

## 1. Configurable concurrency (src/NBomber.YCSB/AerospikeDb)

- [x] 1.1 Add `public static int GetMaxConcurrency(Dictionary<string, string> props)` to `AerospikeHelper`. Verify it compiles.
  - Key absent: return 512.
  - Otherwise: `int.TryParse` the value and return it if it is ≥ 1.
  - Anything else (non-numeric, empty, 0, negative, overflow): throw `ArgumentException($"aerospike.maxconcurrency must be a positive integer, got '{raw}'.", nameof(props))`.
- [x] 1.2 In `AerospikeYcsbClient`, remove `MAX_CONCURRENCY`, add `private readonly int _maxConcurrency` set from `AerospikeHelper.GetMaxConcurrency(props)` before the connection `try` block, and use it for `asyncMaxCommands`, `asyncMaxConnsPerNode` and `BulkInsert` `MaxDegreeOfParallelism`. Keep `MaxCommandAction.DELAY`, all policies, timeouts, retries, Scan and exception handling unchanged; verify with a diff that nothing else changed
- [x] 1.3 Offline tests in `tests/NBomber.YCSB.Tests/AerospikeHelperTests.cs` (not `AerospikeTests`, whose fixture connects to a server):
  - helper: the default is 512 when the prop is absent; a custom value (e.g. 64) is returned.
  - helper, theory over "0", "-5", "abc" and "": throws `ArgumentException`, with `Assert.Contains("aerospike.maxconcurrency", ex.Message)`.
  - constructor: `new AerospikeYcsbClient(props)` with an invalid `aerospike.maxconcurrency` (e.g. "0" and "abc") throws `ArgumentException` naming the setting, before any connection (no server is needed).

  Verify with `dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName~AerospikeHelperTests"`

## 2. Documentation

- [x] 2.1 README.md. Verify by reading the section; other text in it stays unchanged.
  - **Table row (line ~29):** add `.maxconcurrency=512` after `.timeout=10000`.
  - **Aerospike paragraph (line ~59):** replace "The namespace is in memory only (no volume, no replication)" with: data is served from memory (`storage-engine memory`) with a backing file inside the container; there is no volume or replication, so data is lost when the container is removed; write latency is not comparable with stacks that persist writes to disk before acknowledging. Do not say "unlike disk-backed stacks", because Redis/KeyDB are in memory too.
  - **Same paragraph:** replace "Insert uses create-only semantics, so a duplicate key is a failed operation (the Java binding overwrites)." with: insert is create-only, like the Java binding, so a duplicate key is a failed operation. Update and read-modify-write are update-only: they merge fields and return "no data" on a missing key. The Java binding's update uses replace-only, which drops unrelated fields; we keep YCSB partial-update semantics. The bulk load uses replace.
  - **Same paragraph:** replace "The client queues commands above 512 concurrent operations, so keep `--threadcount` at or below 512." with: `aerospike.maxconcurrency` (default 512 when not set; any other value must be a positive integer or the run fails) bounds commands in flight, connections per node and bulk-load parallelism; a `--threadcount` above it queues commands on the client (the wait counts as latency, and small values may also cause timeouts); report a non-default value with results.
  - **Keep** the timeout/no-retries, bulk-load-timeout and truncate sentences.
- [x] 2.2 CLAUDE.md, Gotchas line about new-backend limitations (line ~60): change "is in memory only and has create-only inserts" to "serves data from memory with a backing file inside the container (lost on container removal), has create-only inserts and update-only (not Java's replace-only) updates, and caps concurrency with `aerospike.maxconcurrency` (default 512)". No other edits. Verify by reading the changed line

## 3. Build and checks

- [x] 3.1 Run `dotnet build NBomber.YCSB.sln` and verify 0 errors and no new warnings
- [x] 3.2 Run the offline suite from CLAUDE.md and verify all tests pass
- [x] 3.3 Live check (approved by the user 2026-11-20): via `.worktrees/_shared/live-lock.sh` (acquire/up/down/release; never `-v`), with the Aerospike compose stack fresh, run `dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName~NBomber.YCSB.Tests.AerospikeTests"` and verify all pass (partial update, missing-key update, duplicate insert, BulkInsert replace, more than 512 concurrent operations)

## Workflow follow-up

- Archive the change after code review, OpenSpec verify and user approval.
