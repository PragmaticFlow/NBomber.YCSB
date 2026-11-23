using MySqlConnector;
using NBomber.YCSB.Infra;
using NBomber.YCSB.TiDb;

namespace NBomber.YCSB.Tests
{
    public class TiDbSchemaFixture : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            var client = TiDbTests.CreateClient();
            await client.DeleteAllData();
            await client.InitDb();
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    public class TiDbTests : IClassFixture<TiDbSchemaFixture>
    {
        private const int FIELD_COUNT = 10;
        private const string RAW_CONNECTION = "Server=localhost;Port=4000;User ID=root";

        [Fact]
        public void Workload_A_Shold_Execute_50_Read_50_Update()
        {
            var result = RunWorkload(Workload.A);

            var stats = result.ScenarioStats[0].StepStats;
            AssertNoFailures(stats, "read", "update");

            var readStep = stats.First(s => s.StepName == "read");
            var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

            var updateStep = stats.First(s => s.StepName == "update");
            var updateCount = updateStep.Ok.Request.Count + updateStep.Fail.Request.Count;

            var sum = readCount + updateCount;

            Assert.Equal(50, TestsHelper.RoundToNearest(readCount * 100 / sum, 10));
            Assert.Equal(50, TestsHelper.RoundToNearest(updateCount * 100 / sum, 10));
        }

        [Fact]
        public void Workload_B_Should_Execute_95_Read_5_Update()
        {
            var result = RunWorkload(Workload.B);

            var stats = result.ScenarioStats[0].StepStats;
            AssertNoFailures(stats, "read", "update");

            var readStep = stats.First(s => s.StepName == "read");
            var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

            var updateStep = stats.First(s => s.StepName == "update");
            var updateCount = updateStep.Ok.Request.Count + updateStep.Fail.Request.Count;

            var sum = readCount + updateCount;

            Assert.Equal(95, TestsHelper.RoundToNearest(readCount * 100 / sum, 5));
            Assert.Equal(5, TestsHelper.RoundToNearest(updateCount * 100 / sum, 5));
        }

        [Fact]

        public void Workload_C_Should_Execute_100_Read()
        {
            var result = RunWorkload(Workload.C);

            var stats = result.ScenarioStats[0].StepStats;
            AssertNoFailures(stats, "read");
            var readStep = stats.First(s => s.StepName == "read");
            var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

            Assert.True(readCount > 0);
            Assert.DoesNotContain(stats, s => s.StepName == "update");
        }

        [Fact]
        public void Workload_D_Should_Execute_95_ReadLatest_5_Insert()
        {
            var result = RunWorkload(Workload.D);

            var stats = result.ScenarioStats[0].StepStats;
            AssertNoFailures(stats, "insert");

            // read latest is not asserted: without an acknowledged-insert counter it can read a key whose insert has not run yet ("no data").
            var readLatestStep = stats.First(s => s.StepName == "read latest");
            var readLatestCount = readLatestStep.Ok.Request.Count + readLatestStep.Fail.Request.Count;

            var insertStep = stats.First(s => s.StepName == "insert");
            var insertCount = insertStep.Ok.Request.Count + insertStep.Fail.Request.Count;

            var sum = readLatestCount + insertCount;

            Assert.Equal(95, TestsHelper.RoundToNearest(readLatestCount * 100 / sum, 5));
            Assert.Equal(5, TestsHelper.RoundToNearest(insertCount * 100 / sum, 5));
        }

        [Fact]
        public void Workload_E_Should_Execute_95_Scan_5_Insert()
        {
            var result = RunWorkload(Workload.E);

            var stats = result.ScenarioStats[0].StepStats;

            AssertNoFailures(stats, "scan", "insert");
            var scanStep = stats.First(s => s.StepName == "scan");
            var scanCount = scanStep.Ok.Request.Count + scanStep.Fail.Request.Count;

            var insertStep = stats.First(s => s.StepName == "insert");
            var insertCount = insertStep.Ok.Request.Count + insertStep.Fail.Request.Count;

            var sum = scanCount + insertCount;

            Assert.Equal(95, TestsHelper.RoundToNearest(scanCount * 100 / sum, 5));
            Assert.Equal(5, TestsHelper.RoundToNearest(insertCount * 100 / sum, 5));
        }

        [Fact]
        public void Workload_F_Should_Execute_50_Read_50_ReadModifyWrite()
        {
            var result = RunWorkload(Workload.F);

            var stats = result.ScenarioStats[0].StepStats;

            var readStep = stats.First(s => s.StepName == "read");
            AssertNoFailures(stats, "read", "read-modify-write");
            var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

            var rmwStep = stats.First(s => s.StepName == "read-modify-write");
            var rmwCount = rmwStep.Ok.Request.Count + rmwStep.Fail.Request.Count;

            var sum = readCount + rmwCount;

            Assert.Equal(50, TestsHelper.RoundToNearest(readCount * 100 / sum, 10));
            Assert.Equal(50, TestsHelper.RoundToNearest(rmwCount * 100 / sum, 10));
        }

        // Zipfian hot keys with many threads. pessimistic-auto-commit is false in TiDB v8.5, so autocommit writes are
        // optimistic; TiDB retries conflicting statements itself instead of surfacing write conflicts (9007/8022/8028).
        // Any failed read/update/RMW fails the test; connect timeouts are only counted separately in the message.
        [Theory]
        [InlineData(Workload.A)]
        [InlineData(Workload.F)]
        public void Hot_Key_Contention_At_64_Threads_Has_No_Failed_Update_Or_ReadModifyWrite(Workload workload)
        {
            var result = RunWorkload(workload, threadCount: 64, operationCount: 5000, warmUp: 3);

            AssertNoFailures(result.ScenarioStats[0].StepStats, "read", workload == Workload.A ? "update" : "read-modify-write");
        }

        // ---- Field mapping tests ----
        // Stored values are verified through a separate raw MySQL connection.

        [Fact]
        public async Task Insert_Stores_Each_Value_In_Its_Own_Column()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();

            var response = await client.Insert("ycsb_table", key, values);

            Assert.False(response.IsError);
            Assert.Equal(values, await ReadRawRow(key));
        }

        [Fact]
        public async Task Insert_Duplicate_Key_Fails_And_Keeps_Original_Row()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            await Assert.ThrowsAsync<MySqlException>(() => client.Insert("ycsb_table", key, DistinctValues(shift: 3)));

            Assert.Equal(values, await ReadRawRow(key));
        }

        [Fact]
        public async Task BulkInsert_Stores_Each_Value_In_Its_Own_Column()
        {
            var client = CreateClient();
            var key1 = NewKey();
            var key2 = NewKey();
            var values1 = DistinctValues();
            var values2 = DistinctValues(shift: 5);

            var response = await client.BulkInsert("ycsb_table", new Dictionary<string, Dictionary<string, string>>
            {
                [key1] = values1,
                [key2] = values2
            });

            Assert.False(response.IsError);
            Assert.Equal(values1, await ReadRawRow(key1));
            Assert.Equal(values2, await ReadRawRow(key2));
        }

        [Fact]
        public async Task BulkInsert_Spanning_Several_Chunks_Stores_Every_Row()
        {
            var client = CreateClient();
            var prefix = $"tidb-bulk-{Guid.NewGuid():N}-";
            var data = Enumerable.Range(0, 1201).ToDictionary(i => $"{prefix}{i:D5}", i => DistinctValues(shift: i));

            var response = await client.BulkInsert("ycsb_table", data);

            Assert.False(response.IsError);
            Assert.Equal(1201L, await RawScalar<long>("SELECT COUNT(*) FROM ycsb.ycsb_table WHERE ycsb_key LIKE ?", prefix + "%"));
            Assert.Equal(data[$"{prefix}00777"], await ReadRawRow($"{prefix}00777"));
        }

        [Fact]
        public async Task BulkInsert_Duplicate_Key_Fails()
        {
            var client = CreateClient();
            var key = NewKey();
            await client.Insert("ycsb_table", key, DistinctValues());

            await Assert.ThrowsAsync<MySqlException>(() => client.BulkInsert("ycsb_table",
                new Dictionary<string, Dictionary<string, string>> { [key] = DistinctValues(shift: 1) }));
        }

        [Fact]
        public async Task Read_All_And_Projected_Fields_Report_Matching_Sizes()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var all = await client.Read("ycsb_table", key, null);
            var one = await client.Read("ycsb_table", key, ["field3"]);

            Assert.Equal(TiDbHelper.GetSize(key, values), all.SizeBytes);
            Assert.Equal(
                TiDbHelper.GetSize(key, new Dictionary<string, string> { ["field3"] = values["field3"] }),
                one.SizeBytes);
        }

        [Fact]
        public async Task Read_Missing_Key_Fails_With_No_Data()
        {
            var client = CreateClient();

            var response = await client.Read("ycsb_table", NewKey(), ["field1"]);

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);
        }

        [Fact]
        public async Task Update_One_Field_Leaves_Other_Fields_Unchanged()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var response = await client.Update("ycsb_table", key, new Dictionary<string, string> { ["field4"] = "updated" });

            Assert.False(response.IsError);
            var expected = new Dictionary<string, string>(values) { ["field4"] = "updated" };
            Assert.Equal(expected, await ReadRawRow(key));
        }

        [Fact]
        public async Task Update_With_Identical_Value_Is_Ok()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var response = await client.Update("ycsb_table", key, new Dictionary<string, string> { ["field2"] = values["field2"] });

            Assert.False(response.IsError);
        }

        [Fact]
        public async Task Update_Missing_Key_Fails_And_Creates_No_Row()
        {
            var client = CreateClient();
            var key = NewKey();

            var response = await client.Update("ycsb_table", key, new Dictionary<string, string> { ["field1"] = "x" });

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);
            Assert.Equal(0L, await RawScalar<long>("SELECT COUNT(*) FROM ycsb.ycsb_table WHERE ycsb_key = ?", key));
        }

        [Fact]
        public async Task ReadModifyWrite_Updates_Only_Written_Field_And_Counts_Read_And_Write_Size()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var write = new Dictionary<string, string> { ["field6"] = "rmw-value" };
            var response = await client.ReadModifyWrite("ycsb_table", key, ["field2"], write);

            Assert.False(response.IsError);
            var readSize = TiDbHelper.GetSize(key, new Dictionary<string, string> { ["field2"] = values["field2"] });
            Assert.Equal(readSize + TiDbHelper.GetSize(key, write), response.SizeBytes);

            var expected = new Dictionary<string, string>(values) { ["field6"] = "rmw-value" };
            Assert.Equal(expected, await ReadRawRow(key));
        }

        [Fact]
        public async Task ReadModifyWrite_With_All_Fields_Read_Updates_Only_Written_Fields()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var write = new Dictionary<string, string> { ["field1"] = "first", ["field10"] = "last" };
            var response = await client.ReadModifyWrite("ycsb_table", key, null, write);

            Assert.False(response.IsError);
            Assert.Equal(TiDbHelper.GetSize(key, values) + TiDbHelper.GetSize(key, write), response.SizeBytes);

            var expected = new Dictionary<string, string>(values) { ["field1"] = "first", ["field10"] = "last" };
            Assert.Equal(expected, await ReadRawRow(key));
        }

        [Fact]
        public async Task ReadModifyWrite_Missing_Key_Fails_And_Creates_No_Row()
        {
            var client = CreateClient();
            var key = NewKey();

            var response = await client.ReadModifyWrite("ycsb_table", key, ["field1"], new Dictionary<string, string> { ["field2"] = "x" });

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);
            Assert.Equal(0L, await RawScalar<long>("SELECT COUNT(*) FROM ycsb.ycsb_table WHERE ycsb_key = ?", key));
        }

        // ---- Scan ----

        [Fact]
        public async Task Scan_Starts_At_Start_Key_Inclusive_In_Key_Order_And_Honors_Limit()
        {
            var client = CreateClient();
            var prefix = $"tidb-scan-{Guid.NewGuid():N}-";

            // Inserted in reverse order; row i has a field1 value of length 10 + i, so the summed size identifies the rows returned.
            for (var i = 5; i >= 1; i--)
                await client.Insert("ycsb_table", $"{prefix}{i}", new Dictionary<string, string> { ["field1"] = new string('v', 10 + i) });

            var response = await client.Scan("ycsb_table", $"{prefix}2", 3, null);

            Assert.False(response.IsError);
            Assert.Equal(Enumerable.Range(2, 3).Sum(i => ScanRowSize(prefix, i)), response.SizeBytes);
        }

        [Fact]
        public async Task Scan_With_Field_Subset_Reports_Size_Of_Only_Those_Fields()
        {
            var client = CreateClient();
            var key = $"tidb-scan-{Guid.NewGuid():N}";
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var response = await client.Scan("ycsb_table", key, 1, ["field2", "field5"]);

            Assert.False(response.IsError);
            Assert.Equal(
                TiDbHelper.GetSize(key, new Dictionary<string, string> { ["field2"] = values["field2"], ["field5"] = values["field5"] }),
                response.SizeBytes);
        }

        [Fact]
        public async Task Scan_Orders_Keys_By_Bytes_Not_Case_Insensitively()
        {
            var client = CreateClient();
            var prefix = $"tidb-case-{Guid.NewGuid():N}-";
            await client.Insert("ycsb_table", $"{prefix}a", new Dictionary<string, string> { ["field1"] = "lower" });
            await client.Insert("ycsb_table", $"{prefix}B", new Dictionary<string, string> { ["field1"] = "upper-case" });

            // Byte order puts "B" (0x42) before "a" (0x61), so a scan from "A" finds "B" first; a case-insensitive collation would find "a".
            var response = await client.Scan("ycsb_table", $"{prefix}A", 1, ["field1"]);

            Assert.Equal(
                TiDbHelper.GetSize($"{prefix}B", new Dictionary<string, string> { ["field1"] = "upper-case" }),
                response.SizeBytes);
        }

        // ---- Special values, scan edges, multi-field update, unknown fields ----

        [Fact]
        public async Task Special_Characters_Round_Trip_Unchanged()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            values["field1"] = "it's \"quoted\"";
            values["field2"] = @"back\slash \n \0 %_";
            values["field3"] = "emoji \U0001F600 utf8mb4 \u00e9\u4e2d";
            values["field4"] = "";
            values["field5"] = "; DROP TABLE ycsb.ycsb_table; --";

            Assert.False((await client.Insert("ycsb_table", key, values)).IsError);
            Assert.Equal(values, await ReadRawRow(key));

            var update = new Dictionary<string, string> { ["field6"] = "new'\\ \U0001F600" };
            Assert.False((await client.Update("ycsb_table", key, update)).IsError);
            Assert.Equal(update["field6"], (await ReadRawRow(key))["field6"]);

            var read = await client.Read("ycsb_table", key, ["field3", "field4"]);
            Assert.Equal(
                TiDbHelper.GetSize(key, new Dictionary<string, string> { ["field3"] = values["field3"], ["field4"] = "" }),
                read.SizeBytes);
        }

        [Fact]
        public async Task Scan_With_Count_Larger_Than_Remaining_Rows_Returns_Only_Remaining()
        {
            var database = $"ycsb_tail_{Guid.NewGuid():N}";
            var client = CreateClient($"tidb.database={database}");

            try
            {
                await client.InitDb();
                for (var i = 1; i <= 3; i++)
                    await client.Insert("ycsb_table", $"k{i}", new Dictionary<string, string> { ["field1"] = new string('v', 10 + i) });

                var response = await client.Scan("ycsb_table", "k2", 1000, null);

                Assert.False(response.IsError);
                Assert.Equal(ScanRowSize("k", 2) + ScanRowSize("k", 3), response.SizeBytes);
            }
            finally
            {
                await RawExecute($"DROP DATABASE IF EXISTS {database}");
            }
        }


        [Fact]
        public async Task Scan_On_Empty_Table_Returns_Zero_Bytes()
        {
            var database = $"ycsb_empty_{Guid.NewGuid():N}";
            var client = CreateClient($"tidb.database={database}");

            try
            {
                await client.InitDb();

                var response = await client.Scan("ycsb_table", "user1", 10, null);

                Assert.False(response.IsError);
                Assert.Equal(0L, response.SizeBytes);
            }
            finally
            {
                await RawExecute($"DROP DATABASE IF EXISTS {database}");
            }
        }

        [Fact]
        public async Task Update_Several_Fields_Leaves_Unrelated_Fields_Unchanged()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var update = new Dictionary<string, string> { ["field2"] = "u2", ["field7"] = "u7", ["field10"] = "u10" };
            var response = await client.Update("ycsb_table", key, update);

            Assert.False(response.IsError);
            Assert.Equal(TiDbHelper.GetSize(key, update), response.SizeBytes);
            var expected = new Dictionary<string, string>(values) { ["field2"] = "u2", ["field7"] = "u7", ["field10"] = "u10" };
            Assert.Equal(expected, await ReadRawRow(key));
        }

        [Fact]
        public async Task Insert_And_BulkInsert_With_Unknown_Field_Throw_And_Store_Nothing()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            values["field11"] = "extra";

            await Assert.ThrowsAsync<ArgumentException>(() => client.Insert("ycsb_table", key, values));
            await Assert.ThrowsAsync<ArgumentException>(() => client.BulkInsert("ycsb_table",
                new Dictionary<string, Dictionary<string, string>> { [key] = values }));

            Assert.Equal(0L, await RawScalar<long>("SELECT COUNT(*) FROM ycsb.ycsb_table WHERE ycsb_key = ?", key));
        }

        // ---- Schema / reset ----

        [Fact]
        public async Task Table_Has_Clustered_Primary_Key_And_Binary_Key_Collation()
        {
            Assert.Equal("CLUSTERED", await RawScalar<string>(
                "SELECT tidb_pk_type FROM information_schema.tables WHERE table_schema = 'ycsb' AND table_name = 'ycsb_table'"));
            Assert.Equal("utf8mb4_bin", await RawScalar<string>(
                "SELECT collation_name FROM information_schema.columns WHERE table_schema = 'ycsb' AND table_name = 'ycsb_table' AND column_name = 'ycsb_key'"));
        }

        [Fact]
        public async Task DeleteAllData_Then_InitDb_Gives_Empty_Table_With_Configured_Columns()
        {
            var client = CreateClient();
            await client.Insert("ycsb_table", NewKey(), DistinctValues());

            var response = await client.DeleteAllData();
            Assert.False((await client.InitDb()).IsError);

            Assert.False(response.IsError);
            Assert.Equal(0L, await RawScalar<long>("SELECT COUNT(*) FROM ycsb.ycsb_table"));
            Assert.Equal(
                Enumerable.Range(1, FIELD_COUNT).Select(i => $"field{i}").Append("ycsb_key").ToHashSet(),
                await RawColumns("ycsb"));

            var key = NewKey();
            Assert.False((await client.Insert("ycsb_table", key, DistinctValues())).IsError);
            Assert.False((await client.Read("ycsb_table", key, null)).IsError);
            Assert.False((await client.Scan("ycsb_table", key, 1, null)).IsError);
        }

        [Fact]
        public async Task DeleteAllData_And_InitDb_Can_Repeat()
        {
            var client = CreateClient();

            for (var i = 0; i < 3; i++)
            {
                Assert.False((await client.DeleteAllData()).IsError);
                Assert.False((await client.InitDb()).IsError);
            }

            Assert.Equal(0L, await RawScalar<long>("SELECT COUNT(*) FROM ycsb.ycsb_table"));
        }

        [Fact]
        public async Task DeleteAllData_Keeps_Other_Tables_In_Database()
        {
            var client = CreateClient();
            var otherTable = $"other_{Guid.NewGuid():N}";
            await RawExecute($"CREATE TABLE ycsb.{otherTable} (id VARCHAR(50) PRIMARY KEY, value TEXT)");

            try
            {
                var response = await client.DeleteAllData();

                Assert.False(response.IsError);
                Assert.Equal(1L, await RawScalar<long>(
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'ycsb' AND table_name = ?", otherTable));
            }
            finally
            {
                await RawExecute($"DROP TABLE IF EXISTS ycsb.{otherTable}");
                await client.InitDb();
            }
        }

        [Fact]
        public async Task DeleteAllData_On_Missing_Database_Succeeds_Creates_Nothing_And_InitDb_Then_Works()
        {
            var database = $"ycsb_fresh_{Guid.NewGuid():N}";
            var client = CreateClient($"tidb.database={database}");

            try
            {
                var response = await client.DeleteAllData();

                Assert.False(response.IsError);
                Assert.Equal(0L, await RawScalar<long>(
                    "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = ?", database));

                Assert.False((await client.InitDb()).IsError);
                var key = NewKey();
                Assert.False((await client.Insert("ycsb_table", key, DistinctValues())).IsError);
                Assert.False((await client.Read("ycsb_table", key, null)).IsError);
            }
            finally
            {
                await RawExecute($"DROP DATABASE IF EXISTS {database}");
            }
        }

        [Fact]
        public async Task Changed_FieldCount_Is_Applied_After_DeleteAllData_And_InitDb()
        {
            var client = CreateClient("fieldcount=3");

            try
            {
                Assert.False((await client.DeleteAllData()).IsError);
                Assert.False((await client.InitDb()).IsError);

                Assert.Equal(new HashSet<string> { "ycsb_key", "field1", "field2", "field3" }, await RawColumns("ycsb"));

                var key = NewKey();
                var values = new Dictionary<string, string> { ["field1"] = "a", ["field2"] = "b", ["field3"] = "c" };
                Assert.False((await client.Insert("ycsb_table", key, values)).IsError);
                Assert.Equal(TiDbHelper.GetSize(key, values), (await client.Read("ycsb_table", key, null)).SizeBytes);
            }
            finally
            {
                var defaultClient = CreateClient();
                await defaultClient.DeleteAllData();
                await defaultClient.InitDb();
            }
        }

        [Fact]
        public async Task Unreachable_Server_Throws()
        {
            var client = CreateClient("tidb.port=4999");

            await Assert.ThrowsAnyAsync<Exception>(() => client.InitDb());
        }

        internal static TiDbYcsbClient CreateClient(params string[] extraProps)
        {
            var props = YcsbCliArgs.ParseProps(["tidb.host=localhost", "tidb.port=4000", $"fieldcount={FIELD_COUNT}"]);

            foreach (var (name, value) in YcsbCliArgs.ParseProps(extraProps))
                props[name] = value;

            return new TiDbYcsbClient(props);
        }

        private static string NewKey() => $"tidb-test-{Guid.NewGuid():N}";

        private static void AssertNoFailures(IEnumerable<Contracts.Stats.StepStats> stats, params string[] stepNames)
        {
            var problems = new List<string>();

            foreach (var name in stepNames)
            {
                var step = stats.First(s => s.StepName == name);
                var codes = string.Join("; ", step.Fail.StatusCodes.Select(c => $"{c.StatusCode} x{c.Count} {c.Message}"));

                if (step.Ok.Request.Count == 0 || step.Fail.Request.Count > 0)
                {
                    var timeouts = step.Fail.StatusCodes.Where(c => c.Message.Contains("Connect Timeout expired")).Sum(c => c.Count);
                    problems.Add($"{name}: ok={step.Ok.Request.Count} fail={step.Fail.Request.Count} connect-timeouts={timeouts} [{codes}]");
                }
            }

            Assert.True(problems.Count == 0, string.Join(" | ", problems));
        }

        private static long ScanRowSize(string prefix, int i)
            => TiDbHelper.GetSize($"{prefix}{i}", new Dictionary<string, string> { ["field1"] = new string('v', 10 + i) });

        // field{i} gets a value with a distinct character and length, so a shifted column is detectable.
        private static Dictionary<string, string> DistinctValues(int shift = 0)
            => Enumerable.Range(1, FIELD_COUNT).ToDictionary(
                i => $"field{i}",
                i => new string((char)('a' + (i + shift) % 26), 5 + i));

        private static async Task<MySqlConnection> OpenRaw()
        {
            var conn = new MySqlConnection(RAW_CONNECTION);
            await conn.OpenAsync();
            return conn;
        }

        private static async Task RawExecute(string sql)
        {
            await using var conn = await OpenRaw();
            await using var cmd = new MySqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<T> RawScalar<T>(string sql, params object[] args)
        {
            await using var conn = await OpenRaw();
            await using var cmd = new MySqlCommand(sql, conn);
            foreach (var arg in args)
                cmd.Parameters.Add(new MySqlParameter { Value = arg });

            return (T)Convert.ChangeType((await cmd.ExecuteScalarAsync())!, typeof(T));
        }

        private static async Task<HashSet<string>> RawColumns(string database)
        {
            await using var conn = await OpenRaw();
            await using var cmd = new MySqlCommand(
                "SELECT column_name FROM information_schema.columns WHERE table_schema = ? AND table_name = 'ycsb_table'", conn);
            cmd.Parameters.Add(new MySqlParameter { Value = database });

            var columns = new HashSet<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

            return columns;
        }

        private static async Task<Dictionary<string, string>> ReadRawRow(string key)
        {
            var cols = string.Join(", ", Enumerable.Range(1, FIELD_COUNT).Select(i => $"field{i}"));

            await using var conn = await OpenRaw();
            await using var cmd = new MySqlCommand($"SELECT {cols} FROM ycsb.ycsb_table WHERE ycsb_key = ?", conn);
            cmd.Parameters.Add(new MySqlParameter { Value = key });

            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            return Enumerable.Range(1, FIELD_COUNT).ToDictionary(i => $"field{i}", i => reader.GetString(i - 1));
        }

        private Contracts.Stats.NodeStats RunWorkload(Workload workload, int threadCount = 1, int operationCount = 1000, int warmUp = 0)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = 1000,
                OperationCount = operationCount,
                Db = "tidb",
                ThreadCount = threadCount,
                WarmUp = warmUp,
                FieldCount = 10,
                FieldLength = 100,
                ZeroPadding = 1,
                InsertOrder = "hashed",
                Props = [
                    "tidb.host=localhost",
                    "tidb.port=4000"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);
            props["fieldcount"] = settings.FieldCount.ToString();

            var scenario = new YcsbScenario(new TiDbYcsbClient(props));

            return scenario.Run(settings);
        }
    }
}
