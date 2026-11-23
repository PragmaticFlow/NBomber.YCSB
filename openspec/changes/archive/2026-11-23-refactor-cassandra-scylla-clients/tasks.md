# Tasks

## 1. Cassandra (src/NBomber.YCSB/CassandraDb)

- [x] 1.1 First copy the original `CassandraYcsbClient.cs` and `CassandraHelper.cs` to the session scratchpad. They are untracked, so `git diff` cannot show changes.

  Then, in `CassandraHelper`:
  - replace `ExtractAllFields` and `ExtractFields` with `ExtractFields(Row row, IReadOnlyList<string> fields, int columnOffset)` (same body);
  - change `BuildInsertBindings` to return `object?[]` using exactly `values.TryGetValue(fieldNames[i], out var value); bindings[i + 1] = value;`;
  - add the `ScyllaDbHelper` comments on `BuildInsertBindings` and `BuildUpdateBindings`;
  - leave `BuildUpdateBindings`, `BuildFieldsKey` and the CQL builders' code untouched.

  Verify it compiles
- [x] 1.2 In `CassandraYcsbClient`:
  - **Read method:** add the private `ReadFields` method from design.md Decision 3. Use the condition `fields == null || fields.Count == 0`, call `ToList()` once and use the same list for prepare and extract, and return null only when there is no row.
  - **Read:** "no data" when `result == null`, otherwise `Ok(GetSize(key, result))`.
  - **ReadModifyWrite:** "no data" when `result == null`, otherwise read size, then `ExecuteUpdate`, then `+= GetSize(key, values)`.
  - **Scan:** switch to the merged `ExtractFields`.
  - **BulkInsert:** replace it with `Parallel.ForEachAsync(data, new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency }, async (item, _) => ...)`, using the same prepared insert and bindings.
  - **Comment:** add the prepared-statement-cache comment from `ScyllaYcsbClient`, naming `CassandraHelper.BuildFieldsKey`.
  - Keep the file's existing line endings. Change nothing else. Verify with a diff against the scratchpad copy
- [x] 1.3 Expected: no test edits in `tests/NBomber.YCSB.Tests/CassandraHelperTests.cs`. Verify that the test project builds without new warnings and that `dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName~CassandraHelperTests"` passes

## 2. Out of scope: ScyllaDB

- [x] 2.1 Do not modify `ScyllaDbHelper.cs` or `ScyllaYcsbClient.cs` (user decision: refactor ScyllaDB separately once its live tests can run). Verify they are unchanged with `git diff --stat src/NBomber.YCSB/ScyllaDb`

## 3. Build and checks

- [x] 3.1 Run `dotnet build NBomber.YCSB.sln` and verify 0 errors and no new warnings compared with the build before the change
- [x] 3.2 Run the full offline suite from CLAUDE.md and verify all tests pass
- [x] 3.3 Live check (approved by the user): under `.worktrees/_shared/live-lock.sh` (acquire, up, down without -v, release), with the Cassandra compose stack, run `dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName~NBomber.YCSB.Tests.CassandraTests"`. Verify all pass: reads full and projected, missing key, read-modify-write, scan, BulkInsert, workloads A-F

## Workflow follow-up

- Archive the change after code review, OpenSpec verify and user approval.
