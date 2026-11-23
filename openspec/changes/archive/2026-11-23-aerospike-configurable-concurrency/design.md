# Design

## Context

`AerospikeYcsbClient` (src/NBomber.YCSB/AerospikeDb) uses `private const int MAX_CONCURRENCY = 512` for `AsyncClientPolicy.asyncMaxCommands`, `asyncMaxConnsPerNode` and `BulkInsert`'s `ParallelOptions.MaxDegreeOfParallelism`, with `MaxCommandAction.DELAY`. Props are read in the constructor with `YcsbCliArgs.TryGet`/`TryParseInt` inside a `try` that logs "Failed to connect to Aerospike" and rethrows. The constructor connects to the server, so it cannot be exercised offline. `AerospikeHelper` already holds small static functions with offline tests (`HasNamespace`, `BuildBins`, `GetSize`); `TiDbHelper.GetDatabase(props)` is the existing precedent for reading and validating a prop in a helper.

## Goals / Non-Goals

**Goals:** one prop for the three limits, default 512 (no behaviour change), offline-testable parsing and validation.

**Non-Goals:** deriving the limit from `--threadcount`, separate limits per setting, any change to policies, timeouts, retries, scan or Docker.

## Decisions

1. **Parse in `AerospikeHelper.GetMaxConcurrency(Dictionary<string,string> props)`.**
   - If `props` has no `aerospike.maxconcurrency` key, it returns 512.
   - Otherwise it returns the value when `int.TryParse` succeeds and the result is ≥ 1.
   - In every other case (non-numeric, empty, 0, negative, overflow) it throws `ArgumentException($"aerospike.maxconcurrency must be a positive integer, got '{raw}'.", nameof(props))`.
   - This mirrors the shape of `TiDbHelper.GetDatabase` (read and validate a prop in a helper). The placement differs from the Scylla/Cassandra RF check on purpose (see 2).
   - Alternative: inline parsing in the constructor. It was rejected because the value then cannot be tested without a server.
2. **Call the helper before the connection `try` block** and store the result in a `readonly int _maxConcurrency` field. An invalid value then fails with its own message, not the misleading "Failed to connect to Aerospike". The field replaces the constant in `asyncMaxCommands`, `asyncMaxConnsPerNode` and `BulkInsert`.
3. **Fail fast on any explicit invalid value (user decision).** Unlike the other numeric props, which silently fall back through `TryParseInt`, an explicit `aerospike.maxconcurrency` that is not a positive integer is an error, so a typo cannot silently run with 512. The default applies only when the key is absent. `YcsbCliArgs.TryParseInt` is not used here because it cannot tell "absent" from "invalid".
4. **One value for all three limits.** Each async command in flight holds its own connection, so on a single node the connection limit must be at least the command limit. Keeping them equal, together with bulk parallelism, avoids "no more connections" failures.

## Risks / Trade-offs

- A small value (e.g. 1) serialises the client, and a value below `--threadcount` adds client-side queueing to measured latency → documented; the default keeps current behaviour.
- A very large value opens many connections to one node → user choice, documented.
- It is not verified whether time spent in the client's delay queue counts toward `totalTimeout`. With a small value and a high `--threadcount`, queued commands might time out rather than only wait → the docs say small values can add queueing and possibly timeouts; the default is unchanged.
- Results with a non-default value are only comparable when the value is reported → stated in the README.
- Hot path: unchanged. The value is read once in the constructor.
- No destructive or schema-changing database operation.
