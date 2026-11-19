# Tasks

## 1. PostgreSQL size helper (src/NBomber.YCSB/PosgresNoSQL)

- [x] 1.1 Add `public static long GetSize(string key, string json)` to `PostgresNoSQLHelper`, returning `Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(json)`, and add `using System.Text;` to the helper; `json` stays non-nullable because every call site passes a non-null string; verify it compiles
- [x] 1.2 Replace the inline size calculations in `PostgresNoSQLYcsbClient` (Insert, Read, Scan, both parts of ReadModifyWrite, Update) with `PostgresNoSQLHelper.GetSize`, keeping the same arguments (Scan keeps its `?? ""` fallback; Read and ReadModifyWrite keep their `jsonValue`/`readJson` locals rather than inlining `result.ToString()`, so nullable warnings are unchanged) and removing `using System.Text;` if unused; verify no `Encoding.UTF8` remains in the client
- [x] 1.3 Add offline tests to `tests/NBomber.YCSB.Tests/PostgresNoSQLHelperTests.cs`: ASCII key + JSON gives the sum of their lengths, multi-byte characters are counted in UTF-8 bytes, empty JSON gives the key length; verify they pass with `dotnet test tests/NBomber.YCSB.Tests --filter "FullyQualifiedName~PostgresNoSQLHelperTests"`

## 2. Build and checks

- [x] 2.1 Run `dotnet build NBomber.YCSB.sln` and verify it succeeds with no new warnings
- [x] 2.2 Live check, requires explicit approval to run: with the PosgresNoSQL compose stack up on its own local port, run `dotnet test tests/NBomber.YCSB.Tests --filter "NBomber.YCSB.Tests.PostgresNoSqlTests"` and verify it passes

## Workflow follow-up

- Archive the change after code review, OpenSpec verify and user approval.
