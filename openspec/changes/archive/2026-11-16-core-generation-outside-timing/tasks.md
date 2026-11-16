# Tasks

## 1. Data generation without Bogus (Infra)

- [x] 1.1 In `src/NBomber.YCSB/Infra/DataGenerator.cs`, remove `using Bogus;` and the `_faker` field; use `Random.Shared.Next(1, _fieldCount + 1)` in `CreateValuesToUpdate` and `GetFieldNames`; replace `GenerateFieldValue` with the `string.Create` + `Alphabet` version from design.md; no other changes to the file
- [x] 1.2 In `src/NBomber.YCSB/NBomber.YCSB.csproj`, remove the `Bogus` package reference; verify `grep -rn "Bogus\|Faker" src tests --include=*.cs --include=*.csproj` finds nothing
- [x] 1.3 Add offline tests in a new `tests/NBomber.YCSB.Tests/DataGeneratorTests.cs` (block namespace, `ScanLengthTests` style; build settings with `new YcsbCliArgs { FieldCount = .., FieldLength = .., ReadAllFields = .., WriteAllFields = .. }` and set `ReadAllFields = false` explicitly for single-field read tests, since defaults are ReadAllFields true / WriteAllFields false): with fieldlength 100 every `CreateValues` value has length 100 and only `a`-`z`; with fieldlength 1 values are one lowercase letter; with fieldcount 10 `CreateValues` returns exactly `field1..field10`; `CreateValuesToUpdate` with writeallfields true returns all fields and with false returns one field; over 10,000 draws with fieldcount 10, `GetFieldNames` (readallfields false) and `CreateValuesToUpdate` (writeallfields false) only return `field1..field10` and both `field1` and `field10` occur; with fieldcount 1 the single field is `field1`; `GetFieldNames` returns null when readallfields is true; `Parallel.For` over 10,000 iterations calling `CreateValues` and `GetFieldNames` throws nothing and every result passes the length/alphabet/field-set checks; `Parallel.For` of N = 10,000 calls to `GetKeyNext()` with `InsertOrder = "ordered"` and `RecordCount = 1000` yields N distinct keys, exactly `user1001..user11000`; verify all pass

## 2. Prepare inputs before the timed step (scenario)

- [x] 2.1 In `src/NBomber.YCSB/YcsbScenario.cs`, for each of the six cases move all `dataGen.*` calls before `Step.Run`, wrap each case body in braces, and leave only the `ycsbClient.*` call inside the lambda (read-modify-write without the extra `async`/`await` wrapper); keep step names, call order and everything outside the `switch` unchanged; verified by code review (each lambda contains only the client call)

## 3. Build and regression

- [x] 3.1 Run `dotnet build NBomber.YCSB.sln`; verify 0 errors, no new warnings in touched files, and the NuGet audit passes
- [x] 3.2 Run the offline tests (`dotnet test tests/NBomber.YCSB.Tests --no-build --filter "FullyQualifiedName~DataGeneratorTests|FullyQualifiedName~ScanLengthTests|FullyQualifiedName~RedisHelperTests|FullyQualifiedName~ScyllaDbHelperTests"`); verify all pass. No live-database test is run: database clients are untouched

## Workflow follow-up

- Architect review and user approval before implementation (backend-task Stage 2).
- Optional, requires explicit approval: run one live `Workload_*` test class against the project's local container to confirm the operation mix and step names are unchanged.
- Archive only after explicit user approval.
