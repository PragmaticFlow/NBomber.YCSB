using NBomber.YCSB.CockroachDb;
using NBomber.YCSB.Infra;
using Npgsql;
using System.Text.Json;

namespace NBomber.YCSB.Tests
{
    public class CockroachDbTests
    {
        [Fact]
        public async Task Workload_A_Shold_Execute_50_Read_50_Update()
        {
            await TestsHelper.RetryAsync(() =>
            {
                var result = RunWorkload(Workload.A);

                var stats = result.ScenarioStats[0].StepStats;

                var readStep = stats.First(s => s.StepName == "read");
                var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

                var updateStep = stats.First(s => s.StepName == "update");
                var updateCount = updateStep.Ok.Request.Count + updateStep.Fail.Request.Count;

                var sum = readCount + updateCount;

                var readCountPersent = TestsHelper.RoundToNearest(readCount * 100 / sum, 10);
                var updateCountPersent = TestsHelper.RoundToNearest(updateCount * 100 / sum, 10);

                Assert.Equal(50, readCountPersent);
                Assert.Equal(50, updateCountPersent);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Workload_B_Should_Execute_95_Read_5_Update()
        {
            await TestsHelper.RetryAsync(() =>
            {
                var result = RunWorkload(Workload.B);

                var stats = result.ScenarioStats[0].StepStats;

                var readStep = stats.First(s => s.StepName == "read");
                var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

                var updateStep = stats.First(s => s.StepName == "update");
                var updateCount = updateStep.Ok.Request.Count + updateStep.Fail.Request.Count;

                var sum = readCount + updateCount;

                var readCountPersent = TestsHelper.RoundToNearest(readCount * 100 / sum, 5);
                var updateCountPersent = TestsHelper.RoundToNearest(updateCount * 100 / sum, 5);

                Assert.Equal(95, readCountPersent);
                Assert.Equal(5, updateCountPersent);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Workload_C_Should_Execute_100_Read()
        {
            await TestsHelper.RetryAsync(() =>
            {
                var result = RunWorkload(Workload.C);

                var stats = result.ScenarioStats[0].StepStats;

                var readStep = stats.First(s => s.StepName == "read");
                var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

                var readCountPersent = readCount * 100 / readCount;

                Assert.Equal(100, readCountPersent);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Workload_D_Should_Execute_95_ReadLatest_5_Insert()
        {
            await TestsHelper.RetryAsync(() =>
            {
                var result = RunWorkload(Workload.D);

                var stats = result.ScenarioStats[0].StepStats;

                var readLatestStep = stats.First(s => s.StepName == "read latest");
                var readLatestCount = readLatestStep.Ok.Request.Count + readLatestStep.Fail.Request.Count;

                var insertStep = stats.First(s => s.StepName == "insert");
                var insertCount = insertStep.Ok.Request.Count + insertStep.Fail.Request.Count;

                var sum = readLatestCount + insertCount;

                var readLatestCountPersent = TestsHelper.RoundToNearest(readLatestCount * 100 / sum, 5);
                var insertCountPersent = TestsHelper.RoundToNearest(insertCount * 100 / sum, 5);

                Assert.Equal(95, readLatestCountPersent);
                Assert.Equal(5, insertCountPersent);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Workload_E_Should_Execute_95_Scan_5_Insert()
        {
            await TestsHelper.RetryAsync(() =>
            {
                var result = RunWorkload(Workload.E);

                var stats = result.ScenarioStats[0].StepStats;

                var scanStep = stats.First(s => s.StepName == "scan");
                var scanCount = scanStep.Ok.Request.Count + scanStep.Fail.Request.Count;

                var insertStep = stats.First(s => s.StepName == "insert");
                var insertCount = insertStep.Ok.Request.Count + insertStep.Fail.Request.Count;

                var sum = scanCount + insertCount;

                var scanCountPersent = TestsHelper.RoundToNearest(scanCount * 100 / sum, 5);
                var insertCountPersent = TestsHelper.RoundToNearest(insertCount * 100 / sum, 5);

                Assert.Equal(95, scanCountPersent);
                Assert.Equal(5, insertCountPersent);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Workload_F_Should_Execute_50_Read_50_ReadModifyWrite()
        {
            await TestsHelper.RetryAsync(() =>
            {
                var result = RunWorkload(Workload.F);

                var stats = result.ScenarioStats[0].StepStats;

                var readStep = stats.First(s => s.StepName == "read");
                var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

                var rmwStep = stats.First(s => s.StepName == "read-modify-write");
                var rmwCount = rmwStep.Ok.Request.Count + rmwStep.Fail.Request.Count;

                var sum = readCount + rmwCount;

                var readCountPersent = TestsHelper.RoundToNearest(readCount * 100 / sum, 10);
                var rmwCountPersent = TestsHelper.RoundToNearest(rmwCount * 100 / sum, 10);

                Assert.Equal(50, readCountPersent);
                Assert.Equal(50, rmwCountPersent);

                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Update_Single_Field_Preserves_Other_Fields()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                var original = CreateFields(10, "orig");
                await client.Insert("usertable", key, original);

                var response = await client.Update("usertable", key,
                    new Dictionary<string, string> { ["field3"] = "new_field3" });

                Assert.False(response.IsError);

                var stored = await ReadStored(dataSource, key);

                Assert.Equal(10, stored.Count);
                Assert.Equal("new_field3", stored["field3"]);

                foreach (var (field, value) in original.Where(kv => kv.Key != "field3"))
                    Assert.Equal(value, stored[field]);
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task Update_All_Fields_Replaces_All_Values()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                await client.Insert("usertable", key, CreateFields(10, "orig"));

                var updated = CreateFields(10, "new");
                var response = await client.Update("usertable", key, updated);

                Assert.False(response.IsError);

                var stored = await ReadStored(dataSource, key);

                Assert.Equal(10, stored.Count);

                foreach (var (field, value) in updated)
                    Assert.Equal(value, stored[field]);
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task ReadModifyWrite_Single_Field_Preserves_Other_Fields()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                var original = CreateFields(10, "orig");
                await client.Insert("usertable", key, original);

                var response = await client.ReadModifyWrite("usertable", key,
                    new HashSet<string> { "field1" },
                    new Dictionary<string, string> { ["field7"] = "new_field7" });

                Assert.False(response.IsError);

                var stored = await ReadStored(dataSource, key);

                Assert.Equal(10, stored.Count);
                Assert.Equal("new_field7", stored["field7"]);

                foreach (var (field, value) in original.Where(kv => kv.Key != "field7"))
                    Assert.Equal(value, stored[field]);
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task Update_Multiple_Fields_Merges()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                var original = CreateFields(10, "orig");
                await client.Insert("usertable", key, original);

                var changes = new Dictionary<string, string>
                {
                    ["field7"] = "new_field7",
                    ["field2"] = "new_field2"
                };

                var response = await client.Update("usertable", key, changes);

                Assert.False(response.IsError);

                var stored = await ReadStored(dataSource, key);

                Assert.Equal(10, stored.Count);
                Assert.Equal("new_field7", stored["field7"]);
                Assert.Equal("new_field2", stored["field2"]);

                foreach (var (field, value) in original.Where(kv => !changes.ContainsKey(kv.Key)))
                    Assert.Equal(value, stored[field]);
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task DeleteAllData_Removes_All_Rows_And_Keeps_Table_Usable()
        {
            var client = CreateClient();
            await client.InitDb();

            var key1 = "test_" + Guid.NewGuid();
            var key2 = "test_" + Guid.NewGuid();
            var key3 = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                await client.Insert("usertable", key1, CreateFields(3, "a"));
                await client.Insert("usertable", key2, CreateFields(3, "b"));

                var response = await client.DeleteAllData();

                Assert.False(response.IsError);

                await client.InitDb();

                await using var countCmd = dataSource.CreateCommand(
                    $"SELECT count(*) FROM {CockroachDbYcsbClient.TABLE_NAME}");

                Assert.Equal(0L, (long)(await countCmd.ExecuteScalarAsync())!);

                var indexNames = new List<string>();

                await using var indexCmd = dataSource.CreateCommand(
                    "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = @t");

                indexCmd.Parameters.AddWithValue("@t", CockroachDbYcsbClient.TABLE_NAME);

                await using (var reader = await indexCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        indexNames.Add(reader.GetString(0));
                }

                Assert.Contains($"{CockroachDbYcsbClient.TABLE_NAME}_pkey", indexNames);
                Assert.DoesNotContain($"idx_{CockroachDbYcsbClient.TABLE_NAME}_jsonb", indexNames);

                var insertResponse = await client.Insert("usertable", key3, CreateFields(3, "c"));
                Assert.False(insertResponse.IsError);

                var readResponse = await client.Read("usertable", key3, null);
                Assert.False(readResponse.IsError);
            }
            finally
            {
                await DeleteRow(dataSource, key1);
                await DeleteRow(dataSource, key2);
                await DeleteRow(dataSource, key3);
            }
        }

        [Fact]
        public async Task DeleteAllData_Removes_Leftover_Index_And_Leaves_Only_Primary_Key()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                await client.Insert("usertable", key, CreateFields(3, "orig"));

                await using (var createCmd = dataSource.CreateCommand(
                    $"CREATE INDEX IF NOT EXISTS idx_{CockroachDbYcsbClient.TABLE_NAME}_jsonb " +
                    $"ON {CockroachDbYcsbClient.TABLE_NAME} USING GIN ({CockroachDbYcsbClient.COLUMN_NAME})"))
                {
                    await createCmd.ExecuteNonQueryAsync();
                }

                var response = await client.DeleteAllData();

                Assert.False(response.IsError);

                await client.InitDb();

                await using var indexCmd = dataSource.CreateCommand(
                    "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = @t");

                indexCmd.Parameters.AddWithValue("@t", CockroachDbYcsbClient.TABLE_NAME);

                var indexNames = new List<string>();

                await using (var reader = await indexCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        indexNames.Add(reader.GetString(0));
                }

                Assert.Equal([$"{CockroachDbYcsbClient.TABLE_NAME}_pkey"], indexNames);

                await using var countCmd = dataSource.CreateCommand(
                    $"SELECT count(*) FROM {CockroachDbYcsbClient.TABLE_NAME}");

                Assert.Equal(0L, (long)(await countCmd.ExecuteScalarAsync())!);

                var insertResponse = await client.Insert("usertable", key, CreateFields(3, "new"));

                Assert.False(insertResponse.IsError);
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task DeleteAllData_Leaves_Other_Tables_Untouched()
        {
            var client = CreateClient();
            await client.InitDb();

            var otherTable = "ycsb_other_" + Guid.NewGuid().ToString("N");
            await using var dataSource = CreateDataSource();

            try
            {
                await using (var createCmd = dataSource.CreateCommand(
                    $"CREATE TABLE {otherTable} (id INT PRIMARY KEY); INSERT INTO {otherTable} (id) VALUES (1)"))
                {
                    await createCmd.ExecuteNonQueryAsync();
                }

                var response = await client.DeleteAllData();

                Assert.False(response.IsError);

                await using var countCmd = dataSource.CreateCommand($"SELECT count(*) FROM {otherTable}");

                Assert.Equal(1L, (long)(await countCmd.ExecuteScalarAsync())!);
            }
            finally
            {
                await using var dropCmd = dataSource.CreateCommand($"DROP TABLE IF EXISTS {otherTable}");

                await dropCmd.ExecuteNonQueryAsync();

                await client.InitDb();
            }
        }

        [Fact]
        public async Task Update_Missing_Key_Returns_No_Data_And_Creates_No_Row()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            var response = await client.Update("usertable", key,
                new Dictionary<string, string> { ["field1"] = "x" });

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);

            await AssertRowMissing(dataSource, key);
        }

        [Fact]
        public async Task ReadModifyWrite_Missing_Key_Returns_No_Data_And_Creates_No_Row()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            var response = await client.ReadModifyWrite("usertable", key,
                new HashSet<string> { "field1" },
                new Dictionary<string, string> { ["field2"] = "x" });

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);

            await AssertRowMissing(dataSource, key);
        }

        [Fact]
        public async Task Read_Projected_Fields_Returns_Size()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                await client.Insert("usertable", key, CreateFields(10, "orig"));

                // More than the auto-prepare threshold so both unprepared and prepared executions are covered.
                for (var i = 0; i < 10; i++)
                {
                    var response = await client.Read("usertable", key, new HashSet<string> { i % 2 == 0 ? "field1" : "field7" });

                    Assert.False(response.IsError);
                    Assert.True(response.SizeBytes > 0);
                }

                var scanResponse = await client.Scan("usertable", key, 5, new HashSet<string> { "field1", "field7" });

                Assert.False(scanResponse.IsError);
                Assert.True(scanResponse.SizeBytes > 0);
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task DeleteAllData_Succeeds_On_Empty_Table()
        {
            var client = CreateClient();
            await client.InitDb();

            await using var dataSource = CreateDataSource();

            var firstResponse = await client.DeleteAllData();
            Assert.False(firstResponse.IsError);

            // Second reset runs while the table does not exist (no InitDb in between).
            var secondResponse = await client.DeleteAllData();
            Assert.False(secondResponse.IsError);

            await client.InitDb();

            await using var countCmd = dataSource.CreateCommand(
                $"SELECT count(*) FROM {CockroachDbYcsbClient.TABLE_NAME}");

            Assert.Equal(0L, (long)(await countCmd.ExecuteScalarAsync())!);
        }

        [Fact]
        public async Task DeleteAllData_Then_BulkInsert_Stores_Fresh_Values()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                await client.Insert("usertable", key, CreateFields(5, "old"));

                var response = await client.DeleteAllData();

                Assert.False(response.IsError);

                await client.InitDb();

                var fresh = CreateFields(2, "new");

                await client.BulkInsert("usertable", new Dictionary<string, Dictionary<string, string>> { [key] = fresh });

                var stored = await ReadStored(dataSource, key);

                Assert.Equal(fresh.Count, stored.Count);

                foreach (var (field, value) in fresh)
                    Assert.Equal(value, stored[field]);
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task DeleteAllData_Throws_When_Database_Is_Unreachable()
        {
            // Port 1 is unused, so the connection fails and touches no database.
            var props = CreateProps();
            props["cockroachdb.host"] = "127.0.0.1";
            props["cockroachdb.port"] = "1";

            var client = new CockroachDbYcsbClient(props);

            await Assert.ThrowsAnyAsync<NpgsqlException>(() => client.DeleteAllData());
        }

        [Theory]
        [InlineData(Workload.A)]
        [InlineData(Workload.F)]
        public void Workload_With_Hot_Keys_And_Many_Threads_Has_No_Failed_Writes(Workload workload)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = 100,
                OperationCount = 5000,
                Db = "cockroachdb",
                ThreadCount = 32,
                FieldCount = 5,
                FieldLength = 100,
                ZeroPadding = 5,
                InsertOrder = "ordered",
                ReadAllFields = false,
                WriteAllFields = false,
                Props = ["cockroachdb.host=localhost"]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);
            var client = new CockroachDbYcsbClient(props);

            var result = new YcsbScenario(client).Run(settings);

            var writeStepName = workload == Workload.A ? "update" : "read-modify-write";
            var writeStep = result.ScenarioStats[0].StepStats.First(s => s.StepName == writeStepName);
            var failures = string.Join(", ", writeStep.Fail.StatusCodes.Select(c => $"{c.StatusCode}:{c.Count}"));

            Assert.True(writeStep.Ok.Request.Count > 0);
            Assert.True(writeStep.Fail.Request.Count == 0, $"{writeStepName} failures: {failures}");
        }

        [Fact]
        public async Task BulkInsert_Of_Many_Chunks_Succeeds()
        {
            var client = CreateClient();
            await client.DeleteAllData();
            await client.InitDb();

            var data = Enumerable.Range(0, 200_000)
                .ToDictionary(i => $"user{i:D8}", _ => CreateFields(5, "b"));
            await using var dataSource = CreateDataSource();

            try
            {
                var response = await client.BulkInsert("usertable", data);

                Assert.False(response.IsError);

                await using var countCmd = dataSource.CreateCommand($"SELECT count(*) FROM {CockroachDbYcsbClient.TABLE_NAME}");

                Assert.Equal(200_000L, (long)(await countCmd.ExecuteScalarAsync())!);
            }
            finally
            {
                await client.DeleteAllData();
                await client.InitDb();
            }
        }

        [Fact]
        public async Task Server_Is_CockroachDb()
        {
            await using var dataSource = CreateDataSource();

            await using var cmd = dataSource.CreateCommand("SELECT version()");

            var version = (string?)await cmd.ExecuteScalarAsync();

            Assert.StartsWith("CockroachDB", version);
        }

        [Fact]
        public async Task Insert_Duplicate_Key_Fails()
        {
            var client = CreateClient();
            await client.InitDb();

            var key = "test_" + Guid.NewGuid();
            await using var dataSource = CreateDataSource();

            try
            {
                var first = await client.Insert("usertable", key, CreateFields(3, "a"));
                Assert.False(first.IsError);

                await Assert.ThrowsAnyAsync<PostgresException>(() => client.Insert("usertable", key, CreateFields(3, "b")));
            }
            finally
            {
                await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task Scan_Starts_At_Key_Inclusive_And_Honors_Count()
        {
            var client = CreateClient();
            await client.InitDb();

            var prefix = "zscan_" + Guid.NewGuid().ToString("N");
            var keys = Enumerable.Range(1, 5).Select(i => $"{prefix}_{i}").ToList();
            await using var dataSource = CreateDataSource();

            try
            {
                foreach (var key in keys)
                    await client.Insert("usertable", key, CreateFields(3, "v"));

                var one = await client.Scan("usertable", keys[2], 1, null);
                var two = await client.Scan("usertable", keys[2], 2, null);
                var all = await client.Scan("usertable", keys[2], 100, null);
                var past = await client.Scan("usertable", prefix + "_9", 100, null);

                Assert.True(one.SizeBytes > 0);
                Assert.Equal(one.SizeBytes * 2, two.SizeBytes);
                Assert.Equal(one.SizeBytes * 3, all.SizeBytes);
                Assert.False(past.IsError);
                Assert.Equal(0, past.SizeBytes);
            }
            finally
            {
                foreach (var key in keys)
                    await DeleteRow(dataSource, key);
            }
        }

        [Fact]
        public async Task BulkInsert_Stores_All_Records_Across_Chunks()
        {
            var client = CreateClient();
            await client.InitDb();

            var prefix = "zbulk_" + Guid.NewGuid().ToString("N");
            var data = Enumerable.Range(0, 2500)
                .ToDictionary(i => $"{prefix}_{i}", i => CreateFields(3, "b"));
            await using var dataSource = CreateDataSource();

            try
            {
                var response = await client.BulkInsert("usertable", data);

                Assert.False(response.IsError);

                await using var countCmd = dataSource.CreateCommand(
                    $"SELECT count(*) FROM {CockroachDbYcsbClient.TABLE_NAME} WHERE {CockroachDbYcsbClient.PRIMARY_KEY} LIKE @p");

                countCmd.Parameters.AddWithValue("@p", prefix + "%");

                Assert.Equal(2500L, (long)(await countCmd.ExecuteScalarAsync())!);
            }
            finally
            {
                await using var delCmd = dataSource.CreateCommand(
                    $"DELETE FROM {CockroachDbYcsbClient.TABLE_NAME} WHERE {CockroachDbYcsbClient.PRIMARY_KEY} LIKE @p");

                delCmd.Parameters.AddWithValue("@p", prefix + "%");

                await delCmd.ExecuteNonQueryAsync();
            }
        }

        private static Dictionary<string, string> CreateProps()
        {
            return YcsbCliArgs.ParseProps([
                "cockroachdb.host=localhost",
                "cockroachdb.port=26257",
                "cockroachdb.database=defaultdb",
                "cockroachdb.user=root"
            ]);
        }

        private static CockroachDbYcsbClient CreateClient()
        {
            return new CockroachDbYcsbClient(CreateProps());
        }

        private static NpgsqlDataSource CreateDataSource()
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 26257,
                Database = "defaultdb",
                Username = "root"
            };

            return NpgsqlDataSource.Create(builder);
        }

        private static Dictionary<string, string> CreateFields(int count, string prefix)
        {
            return Enumerable.Range(0, count)
                .ToDictionary(i => $"field{i}", i => $"{prefix}_value_{i}");
        }

        private static async Task<Dictionary<string, string>> ReadStored(NpgsqlDataSource dataSource, string key)
        {
            await using var cmd = dataSource.CreateCommand(
                $"SELECT {CockroachDbYcsbClient.COLUMN_NAME}::text FROM {CockroachDbYcsbClient.TABLE_NAME} WHERE {CockroachDbYcsbClient.PRIMARY_KEY} = @k");

            cmd.Parameters.AddWithValue("@k", key);

            var json = (string?)await cmd.ExecuteScalarAsync();

            Assert.NotNull(json);

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        }

        private static async Task AssertRowMissing(NpgsqlDataSource dataSource, string key)
        {
            await using var cmd = dataSource.CreateCommand(
                $"SELECT count(*) FROM {CockroachDbYcsbClient.TABLE_NAME} WHERE {CockroachDbYcsbClient.PRIMARY_KEY} = @k");

            cmd.Parameters.AddWithValue("@k", key);

            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }

        private static async Task DeleteRow(NpgsqlDataSource dataSource, string key)
        {
            await using var cmd = dataSource.CreateCommand(
                $"DELETE FROM {CockroachDbYcsbClient.TABLE_NAME} WHERE {CockroachDbYcsbClient.PRIMARY_KEY} = @k");

            cmd.Parameters.AddWithValue("@k", key);

            await cmd.ExecuteNonQueryAsync();
        }

        private Contracts.Stats.NodeStats RunWorkload(Workload workload)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = 1000,
                OperationCount = 1000,
                Db = "cockroachdb",
                ThreadCount = 1,
                FieldCount = 5,
                FieldLength = 100,
                ZeroPadding = 5,
                InsertOrder = "ordered",
                ReadAllFields = false,
                WriteAllFields = false,
                Props = [
                    "cockroachdb.host=localhost",
                    "cockroachdb.port=26257",
                    "cockroachdb.database=defaultdb",
                    "cockroachdb.user=root"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var cockroachClient = new CockroachDbYcsbClient(props);

            var scenario = new YcsbScenario(cockroachClient);

            return scenario.Run(settings);
        }
    }
}
