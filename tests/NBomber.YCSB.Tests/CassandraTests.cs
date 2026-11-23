using NBomber.YCSB.Infra;
using NBomber.YCSB.CassandraDb;

namespace NBomber.YCSB.Tests
{
    public class CassandraSchemaFixture : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            // Fail the whole class fast if the port is not served by Cassandra 5 (9042 is the default CQL port of other databases).
            var version = CassandraTests.RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement("SELECT release_version FROM system.local"))
                .First().GetValue<string>("release_version");
            Assert.StartsWith("5.", version);

            await CassandraTests.CreateClient().InitDb();
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    public class CassandraTests : IClassFixture<CassandraSchemaFixture>
    {
        private const int FIELD_COUNT = 10;

        internal static readonly Lazy<Cassandra.ISession> RAW_SESSION = new(
            () => Cassandra.Cluster.Builder().AddContactPoint("127.0.0.1").WithPort(9043)
                .WithQueryOptions(new Cassandra.QueryOptions().SetConsistencyLevel(Cassandra.ConsistencyLevel.Quorum)).Build().Connect());

        [Fact]
        public void Workload_A_Shold_Execute_50_Read_50_Update()
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
        }

        [Fact]
        public void Workload_B_Should_Execute_95_Read_5_Update()
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
        }

        [Fact]
        public void Workload_C_Should_Execute_100_Read()
        {
            var result = RunWorkload(Workload.C);

            var stats = result.ScenarioStats[0].StepStats;

            var readStep = stats.First(s => s.StepName == "read");
            var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

            var readCountPersent = readCount * 100 / readCount;

            Assert.Equal(100, readCountPersent);
        }

        [Fact]
        public void Workload_D_Should_Execute_95_ReadLatest_5_Insert()
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
        }

        [Fact]
        public void Workload_E_Should_Execute_95_Scan_5_Insert()
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
        }

        [Fact]
        public void Workload_F_Should_Execute_50_Read_50_ReadModifyWrite()
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
        }

        // ---- Field mapping tests ----
        // Stored values are verified through a separate raw CQL session.

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
        public async Task Read_All_And_Projected_Fields_Report_Matching_Sizes()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var all = await client.Read("ycsb_table", key, null);
            var one = await client.Read("ycsb_table", key, ["field3"]);

            Assert.Equal(CassandraHelper.GetSize(key, values), all.SizeBytes);
            Assert.Equal(
                CassandraHelper.GetSize(key, new Dictionary<string, string> { ["field3"] = values["field3"] }),
                one.SizeBytes);
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
        public async Task ReadModifyWrite_Updates_Only_Written_Field_And_Counts_Read_And_Write_Size()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var write = new Dictionary<string, string> { ["field6"] = "rmw-value" };
            var response = await client.ReadModifyWrite("ycsb_table", key, ["field2"], write);

            Assert.False(response.IsError);
            var readSize = CassandraHelper.GetSize(key, new Dictionary<string, string> { ["field2"] = values["field2"] });
            Assert.Equal(readSize + CassandraHelper.GetSize(key, write), response.SizeBytes);

            var expected = new Dictionary<string, string>(values) { ["field6"] = "rmw-value" };
            Assert.Equal(expected, await ReadRawRow(key));
        }

        [Fact]
        public async Task ReadModifyWrite_With_All_Fields_Read_Updates_Only_Written_Field()
        {
            var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var write = new Dictionary<string, string> { ["field1"] = "first", ["field10"] = "last" };
            var response = await client.ReadModifyWrite("ycsb_table", key, null, write);

            Assert.False(response.IsError);
            Assert.Equal(CassandraHelper.GetSize(key, values) + CassandraHelper.GetSize(key, write), response.SizeBytes);

            var expected = new Dictionary<string, string>(values) { ["field1"] = "first", ["field10"] = "last" };
            Assert.Equal(expected, await ReadRawRow(key));
        }

        [Fact]
        public async Task Scan_All_Fields_Reports_Size_Of_First_Row_In_Token_Order()
        {
            var client = CreateClient();
            var key = NewKey();
            await client.Insert("ycsb_table", key, DistinctValues());

            var response = await client.Scan("ycsb_table", key, 1, null);

            // Scan is ordered by TOKEN(key), not by key, so the row found may be another key.
            var (firstKey, firstRow) = await ReadRawFirstRowFromToken(key);
            Assert.False(response.IsError);
            Assert.Equal(CassandraHelper.GetSize(firstKey, firstRow), response.SizeBytes);
        }

        [Fact]
        public async Task Scan_With_Field_Subset_Reports_Size_Of_Only_Those_Fields()
        {
            var client = CreateClient();
            var key = NewKey();
            await client.Insert("ycsb_table", key, DistinctValues());

            var response = await client.Scan("ycsb_table", key, 1, ["field2", "field5"]);

            var (firstKey, firstRow) = await ReadRawFirstRowFromToken(key);
            var subset = firstRow.Where(kv => kv.Key is "field2" or "field5").ToDictionary(kv => kv.Key, kv => kv.Value);
            Assert.False(response.IsError);
            Assert.Equal(CassandraHelper.GetSize(firstKey, subset), response.SizeBytes);
        }

        [Fact]
        public async Task DeleteAllData_Then_InitDb_Gives_Empty_Table_With_Configured_Columns()
        {
            var client = CreateClient();
            await client.Insert("ycsb_table", NewKey(), DistinctValues());

            var response = await client.DeleteAllData();
            Assert.False((await client.InitDb()).IsError);

            Assert.False(response.IsError);
            Assert.Empty(RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement("SELECT ycsb_key FROM ycsb.ycsb_table")));

            var columns = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                    "SELECT column_name FROM system_schema.columns WHERE keyspace_name = 'ycsb' AND table_name = 'ycsb_table'"))
                .Select(r => r.GetValue<string>("column_name"))
                .ToHashSet();
            var expected = Enumerable.Range(1, FIELD_COUNT).Select(i => $"field{i}").Append("ycsb_key").ToHashSet();
            Assert.Equal(expected, columns);

            var key = NewKey();
            var values = DistinctValues();
            Assert.False((await client.Insert("ycsb_table", key, values)).IsError);
            Assert.False((await client.Read("ycsb_table", key, null)).IsError);
            Assert.False((await client.Scan("ycsb_table", key, 1, null)).IsError);
        }

        [Fact]
        public async Task DeleteAllData_Keeps_Other_Tables_In_Keyspace()
        {
            var client = CreateClient();
            var otherTable = $"other_{Guid.NewGuid():N}";
            RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                $"CREATE TABLE ycsb.{otherTable} (id text PRIMARY KEY, value text)"));

            try
            {
                var response = await client.DeleteAllData();

                Assert.False(response.IsError);
                var found = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                    "SELECT table_name FROM system_schema.tables WHERE keyspace_name = 'ycsb' AND table_name = ?", otherTable));
                Assert.Single(found);
            }
            finally
            {
                RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement($"DROP TABLE IF EXISTS ycsb.{otherTable}"));
                await client.InitDb();
            }
        }

        [Fact]
        public async Task DeleteAllData_On_Fresh_Keyspace_Succeeds_Creates_Nothing_And_InitDb_Then_Works()
        {
            var keyspace = $"ycsb_fresh_{Guid.NewGuid():N}";
            var client = new CassandraYcsbClient(YcsbCliArgs.ParseProps([
                "cassandra.hosts=127.0.0.1",
                "cassandra.port=9043",
                $"cassandra.keyspace={keyspace}",
                "cassandra.replicationfactor=1",
                $"fieldcount={FIELD_COUNT}"
            ]));

            try
            {
                var response = await client.DeleteAllData();

                Assert.False(response.IsError);
                Assert.Empty(RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                    "SELECT keyspace_name FROM system_schema.keyspaces WHERE keyspace_name = ?", keyspace)));

                Assert.False((await client.InitDb()).IsError);
                var key = NewKey();
                Assert.False((await client.Insert("ycsb_table", key, DistinctValues())).IsError);
                Assert.False((await client.Read("ycsb_table", key, null)).IsError);
            }
            finally
            {
                RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement($"DROP KEYSPACE IF EXISTS {keyspace}"));
            }
        }

        [Fact]
        public void Test_Session_Is_Connected_To_Cassandra_5()
        {
            var row = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement("SELECT release_version FROM system.local")).First();

            Assert.StartsWith("5.", row.GetValue<string>("release_version"));
        }

        [Fact]
        public async Task Client_Uses_Cassandra_Defaults_Port_9043_And_Replication_Factor_1()
        {
            var keyspace = $"ycsb_rf_{Guid.NewGuid():N}";
            var client = new CassandraYcsbClient(YcsbCliArgs.ParseProps([
                "cassandra.hosts=127.0.0.1",
                $"cassandra.keyspace={keyspace}"
            ]));

            try
            {
                await client.InitDb();

                var replication = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                        "SELECT replication FROM system_schema.keyspaces WHERE keyspace_name = ?", keyspace))
                    .First().GetValue<IDictionary<string, string>>("replication");
                Assert.Equal("1", replication["replication_factor"]);
            }
            finally
            {
                RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement($"DROP KEYSPACE IF EXISTS {keyspace}"));
            }
        }

        [Fact]
        public void Client_Fails_When_Cassandra_Is_Unreachable()
        {
            Assert.ThrowsAny<Exception>(() => new CassandraYcsbClient(YcsbCliArgs.ParseProps([
                "cassandra.hosts=127.0.0.1",
                "cassandra.port=9999"
            ])));
        }

        [Fact]
        public async Task Update_Missing_Key_Upserts_The_Row()
        {
            var client = CreateClient();
            var key = NewKey();

            var response = await client.Update("ycsb_table", key, new Dictionary<string, string> { ["field2"] = "upserted" });

            Assert.False(response.IsError);
            var row = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                "SELECT field1, field2 FROM ycsb.ycsb_table WHERE ycsb_key = ?", key)).First();
            Assert.Null(row.GetValue<string>("field1"));
            Assert.Equal("upserted", row.GetValue<string>("field2"));
        }

        [Fact]
        public async Task Read_Missing_Key_Reports_No_Data()
        {
            var client = CreateClient();

            var response = await client.Read("ycsb_table", NewKey(), null);

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);
        }

        [Fact]
        public async Task Scan_Respects_Count_And_Sums_Rows_In_Token_Order()
        {
            var client = CreateClient();
            var keys = Enumerable.Range(0, 5).Select(_ => NewKey()).ToList();
            foreach (var key in keys)
                await client.Insert("ycsb_table", key, DistinctValues());

            // The first row in token order, so at least the 5 inserted rows are at or after it.
            var startKey = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement("SELECT ycsb_key FROM ycsb.ycsb_table LIMIT 1")).First().GetValue<string>(0);
            var response = await client.Scan("ycsb_table", startKey, 3, null);

            var cols = string.Join(", ", Enumerable.Range(1, FIELD_COUNT).Select(i => $"field{i}"));
            var rows = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                $"SELECT ycsb_key, {cols} FROM ycsb.ycsb_table WHERE TOKEN(ycsb_key) >= TOKEN(?) LIMIT 3", startKey)).ToList();
            Assert.Equal(3, rows.Count);
            var expected = rows.Sum(r => CassandraHelper.GetSize(
                r.GetValue<string>(0),
                Enumerable.Range(1, FIELD_COUNT).Where(i => r.GetValue<string>(i) != null).ToDictionary(i => $"field{i}", i => r.GetValue<string>(i))));
            Assert.False(response.IsError);
            Assert.Equal(expected, response.SizeBytes);
        }

        // Port and replication factor come from the "cassandra" defaults (9043, RF 1).
        internal static CassandraYcsbClient CreateClient()
            => new(YcsbCliArgs.ParseProps([
                "cassandra.hosts=127.0.0.1",
                $"fieldcount={FIELD_COUNT}"
            ]));

        private static string NewKey() => $"cassandra-test-{Guid.NewGuid():N}";

        // field{i} gets a value with a distinct character and length, so a shifted column is detectable.
        private static Dictionary<string, string> DistinctValues(int shift = 0)
            => Enumerable.Range(1, FIELD_COUNT).ToDictionary(
                i => $"field{i}",
                i => new string((char)('a' + (i + shift) % 26), 5 + i));

        private static Task<Dictionary<string, string>> ReadRawRow(string key)
        {
            var cols = string.Join(", ", Enumerable.Range(1, FIELD_COUNT).Select(i => $"field{i}"));
            var rs = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                $"SELECT {cols} FROM ycsb.ycsb_table WHERE ycsb_key = ?", key));
            var row = rs.First();

            return Task.FromResult(Enumerable.Range(1, FIELD_COUNT)
                .ToDictionary(i => $"field{i}", i => row.GetValue<string>(i - 1)));
        }

        private static Task<(string Key, Dictionary<string, string> Fields)> ReadRawFirstRowFromToken(string startKey)
        {
            var cols = string.Join(", ", Enumerable.Range(1, FIELD_COUNT).Select(i => $"field{i}"));
            var rs = RAW_SESSION.Value.Execute(new Cassandra.SimpleStatement(
                $"SELECT ycsb_key, {cols} FROM ycsb.ycsb_table WHERE TOKEN(ycsb_key) >= TOKEN(?) LIMIT 1", startKey));
            var row = rs.First();

            var fields = new Dictionary<string, string>();
            for (var i = 1; i <= FIELD_COUNT; i++)
            {
                var v = row.GetValue<string>(i);
                if (v != null) fields[$"field{i}"] = v;
            }

            return Task.FromResult((row.GetValue<string>(0), fields));
        }

        private Contracts.Stats.NodeStats RunWorkload(Workload workload)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = 1000,
                OperationCount = 1000,
                Db = "cassandra",
                ThreadCount = 1,
                FieldCount = 10,
                FieldLength = 100,
                ZeroPadding = 1,
                InsertOrder = "hashed",
                Props = [
                    "cassandra.hosts=127.0.0.1",
                    "cassandra.port=9043"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var cassandraClient = new CassandraYcsbClient(props);

            var scenario = new YcsbScenario(cassandraClient);

            return scenario.Run(settings);
        }
    }
}
