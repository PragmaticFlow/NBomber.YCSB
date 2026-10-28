using NBomber.YCSB.Infra;
using NBomber.YCSB.PosgresNoSQL;
using Npgsql;
using System.Text.Json;

namespace NBomber.YCSB.Tests
{
    public class PostgresNoSqlTests
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

                await using var countCmd = dataSource.CreateCommand(
                    $"SELECT count(*) FROM {PostgresNoSQLYcsbClient.TABLE_NAME}");

                Assert.Equal(0L, (long)(await countCmd.ExecuteScalarAsync())!);

                var indexNames = new List<string>();

                await using var indexCmd = dataSource.CreateCommand(
                    "SELECT indexname FROM pg_indexes WHERE schemaname = 'public' AND tablename = @t");

                indexCmd.Parameters.AddWithValue("@t", PostgresNoSQLYcsbClient.TABLE_NAME);

                await using (var reader = await indexCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        indexNames.Add(reader.GetString(0));
                }

                Assert.Contains($"{PostgresNoSQLYcsbClient.TABLE_NAME}_pkey", indexNames);
                Assert.Contains($"idx_{PostgresNoSQLYcsbClient.TABLE_NAME}_jsonb", indexNames);

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
        public async Task DeleteAllData_Succeeds_On_Empty_Table()
        {
            var client = CreateClient();
            await client.InitDb();

            await using var dataSource = CreateDataSource();

            var firstResponse = await client.DeleteAllData();
            Assert.False(firstResponse.IsError);

            var secondResponse = await client.DeleteAllData();
            Assert.False(secondResponse.IsError);

            await using var countCmd = dataSource.CreateCommand(
                $"SELECT count(*) FROM {PostgresNoSQLYcsbClient.TABLE_NAME}");

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

        private static Dictionary<string, string> CreateProps()
        {
            return YcsbCliArgs.ParseProps([
                "postgres.host=localhost",
                "postgres.port=5432",
                "postgres.database=mydb",
                "postgres.user=myuser",
                "postgres.password=mysecretpassword"
            ]);
        }

        private static PostgresNoSQLYcsbClient CreateClient()
        {
            return new PostgresNoSQLYcsbClient(CreateProps());
        }

        // Separate data source (same connection settings as the client) used to verify stored values directly.
        private static NpgsqlDataSource CreateDataSource()
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = "localhost",
                Port = 5432,
                Database = "mydb",
                Username = "myuser",
                Password = "mysecretpassword"
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
                $"SELECT {PostgresNoSQLYcsbClient.COLUMN_NAME}::text FROM {PostgresNoSQLYcsbClient.TABLE_NAME} WHERE {PostgresNoSQLYcsbClient.PRIMARY_KEY} = @k");

            cmd.Parameters.AddWithValue("@k", key);

            var json = (string?)await cmd.ExecuteScalarAsync();

            Assert.NotNull(json);

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
        }

        // Removes only the row created by the test.
        private static async Task DeleteRow(NpgsqlDataSource dataSource, string key)
        {
            await using var cmd = dataSource.CreateCommand(
                $"DELETE FROM {PostgresNoSQLYcsbClient.TABLE_NAME} WHERE {PostgresNoSQLYcsbClient.PRIMARY_KEY} = @k");

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
                Db = "postgres",
                ThreadCount = 1,
                FieldCount = 5,
                FieldLength = 100,
                ZeroPadding = 5,
                InsertOrder = "ordered",
                ReadAllFields = false,
                WriteAllFields = false,
                Props = [
                    "postgres.host=localhost",
                    "postgres.port=5432",
                    "postgres.database=mydb",
                    "postgres.user=myuser",
                    "postgres.password=mysecretpassword"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var postgresClient = new PostgresNoSQLYcsbClient(props);

            var scenario = new YcsbScenario(postgresClient);

            return scenario.Run(settings);
        }
    }
}
