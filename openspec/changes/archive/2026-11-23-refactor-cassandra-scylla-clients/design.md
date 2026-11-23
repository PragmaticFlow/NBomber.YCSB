# Design

## Context

`CassandraYcsbClient`/`CassandraHelper` are line-for-line copies of `ScyllaYcsbClient`/`ScyllaDbHelper`. Current state of the Cassandra copy:
- `ExtractAllFields(Row, string[], int)` and `ExtractFields(Row, List<string>, int)` have the same body.
- `BuildInsertBindings` returns `object[]` using `null!` for missing fields.
- `Read` and `ReadModifyWrite` each repeat the full/projected branch (statement, `ExecuteAsync`, `FirstOrDefault`, "no data", extract).
- `BulkInsert` does `data.Select(async ...)` + `SemaphoreSlim(_maxConcurrency)` + `Task.WhenAll`, so `Task.WhenAll` materialises one async state machine and one semaphore waiter per record (the same pattern fixed in the Aerospike client).

The user limited this change to Cassandra. ScyllaDB is refactored separately when its live tests can run.

## Goals / Non-Goals

**Goals:** less duplication, no nullable suppression and a bounded bulk-load task count in the Cassandra client.

**Non-Goals:**
- any change to `ScyllaDbHelper` or `ScyllaYcsbClient`;
- any change to CQL, statement caching, consistency, RF, timeouts, retries, props or measured behaviour;
- making `_maxConcurrency` configurable or a `const`;
- new abstractions shared across databases or around `ISession`.

## Decisions

1. **`ExtractFields(Row row, IReadOnlyList<string> fields, int columnOffset)`** replaces both methods, with the same body (capacity, column `columnOffset + i`, skip null). `string[]` and `List<string>` both implement `IReadOnlyList<string>`.
   - Call sites: lines 127, 139, 154, 165, 196 and 197 of the client.
   - The Scan ternary compiles unchanged.
2. **`BuildInsertBindings` returns `object?[]`**, using exactly `values.TryGetValue(fieldNames[i], out var value); bindings[i + 1] = value;`.
   - `PreparedStatement.Bind(params object[])` comes from the nullable-oblivious driver assembly. An `object?[]` argument passes in normal params form (identity conversion, so the array is not wrapped), with no cast and no caller change.
   - A miss writes `null`, bound as CQL null as today.
   - "No new warnings" is checked by the build.
3. **One private read method in the client:**
   ```csharp
   private async Task<Dictionary<string, string>?> ReadFields(string key, HashSet<string>? fields)
   {
       IReadOnlyList<string> fieldList;
       PreparedStatement ps;
       if (fields == null || fields.Count == 0)
       {
           fieldList = _fieldNames;
           ps = _readPs ?? await PrepareRead();
       }
       else
       {
           fieldList = fields.ToList();
           ps = await GetProjectedReadPs(fieldList);
       }
       var row = (await _session.ExecuteAsync(ps.Bind(key))).FirstOrDefault();
       return row == null ? null : CassandraHelper.ExtractFields(row, fieldList, columnOffset: 0);
   }
   ```
   - The condition is exactly `fields == null || fields.Count == 0`, as today.
   - `ToList()` is called once, and the same list is used to prepare and to extract, so the field order matches.
   - Callers check `result == null` for "no data", not `Count == 0`. A row that exists with all fields null stays Ok, with size = key bytes.
   - `Read` returns "no data" or `Ok(GetSize(key, result))`.
   - `ReadModifyWrite`: "no data" on null. Otherwise `size = GetSize(key, result)`, then `ExecuteUpdate`, then `size += GetSize(key, values)`.
   - `Scan` only switches to the merged `ExtractFields`.
4. **`BulkInsert` uses `Parallel.ForEachAsync(data, new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency }, async (item, _) => await _session.ExecuteAsync((_insertPs ?? await PrepareInsert()).Bind(CassandraHelper.BuildInsertBindings(item.Key, _fieldNames, item.Value))))`.** This follows the `AerospikeYcsbClient.BulkInsert` pattern.
   - At most 512 in flight; bounded memory.
   - `await` rethrows one unwrapped exception, as today.
   - After the first failure, no new items start, and in-flight items complete (approved by the user). The driver's `ExecuteAsync(IStatement)` has no token, so the token parameter is `_`.
   - The per-item `_insertPs ?? await PrepareInsert()` stays as in `Insert`. Its race is benign and only reached if `InitDb` was skipped.
5. **Comments:** add to the Cassandra files the comments that exist only in the ScyllaDB copy:
   - the prepared-statement-cache comment in `CassandraYcsbClient`, naming `CassandraHelper.BuildFieldsKey`;
   - the `ScyllaDbHelper` comments on `BuildInsertBindings` and `BuildUpdateBindings`, in `CassandraHelper`.

   The existing `_maxConcurrency` comment stays in Cassandra; Scylla is not touched.

## Risks / Trade-offs

- **Temporary structural divergence from ScyllaDB:** CLAUDE.md describes Cassandra as a line-for-line copy of ScyllaDB. After this change the two differ in structure (not in measured behaviour) until the separate ScyllaDB refactor. → Stated in the proposal; the final report flags the CLAUDE.md wording for the user.
- **Failure timing in the bulk load:** a failing load stops starting inserts after the first failure, instead of attempting every record. The init fails either way and measured results are unaffected. Approved by the user.
- **No offline test for `ExtractFields`, the read path or `BulkInsert`:** a driver `Row`/`ISession` cannot be faked without a new seam, which the constraints forbid. Coverage comes from the existing live `CassandraTests`:
  - reads (full and projected), missing key, read-modify-write, scan;
  - `BulkInsert_*`;
  - `Workload_*`, whose `RecordCount` of 1000 is above 512, so the bounded path is exercised.

  The 512 bound itself is enforced by `MaxDegreeOfParallelism`.
- **Hot path:** one extra async state machine per Read/RMW on the measured path. That is negligible next to a network round trip.
- **Untracked Cassandra files:** `git diff` cannot show the Cassandra changes → snapshot the originals to the scratchpad before editing and diff against that copy.
