using NBomber.YCSB.Infra;
using NBomber.YCSB.ScyllaDb;

namespace NBomber.YCSB.Tests
{
    public class ScyllaDbTests
    {
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
        // These need a running ScyllaDB where ycsb.ycsb_table already exists with the default 10
        // field columns (e.g. after a workload run / InitDb). They use unique keys and never call
        // InitDb or DeleteAllData. Stored values are verified through a separate raw CQL session.

        private const int FieldCount = 10;

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

            Assert.Equal(ScyllaDbHelper.GetSize(key, values), all.SizeBytes);
            Assert.Equal(
                ScyllaDbHelper.GetSize(key, new Dictionary<string, string> { ["field3"] = values["field3"] }),
                one.SizeBytes);
        }

        [Fact]
        public async Task Read_Missing_Key_Fails()
        {
            var client = CreateClient();

            var response = await client.Read("ycsb_table", NewKey(), ["field1"]);

            Assert.True(response.IsError);
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
            var readSize = ScyllaDbHelper.GetSize(key, new Dictionary<string, string> { ["field2"] = values["field2"] });
            Assert.Equal(readSize + ScyllaDbHelper.GetSize(key, write), response.SizeBytes);

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
            Assert.Equal(ScyllaDbHelper.GetSize(key, values) + ScyllaDbHelper.GetSize(key, write), response.SizeBytes);

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
            Assert.Equal(ScyllaDbHelper.GetSize(firstKey, firstRow), response.SizeBytes);
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
            Assert.Equal(ScyllaDbHelper.GetSize(firstKey, subset), response.SizeBytes);
        }

        private static ScyllaYcsbClient CreateClient()
            => new(YcsbCliArgs.ParseProps([
                "scylladb.host=localhost",
                "scylladb.port=9042",
                $"fieldcount={FieldCount}"
            ]));

        private static string NewKey() => $"scylla-test-{Guid.NewGuid():N}";

        // field{i} gets a value with a distinct character and length, so a shifted column is detectable.
        private static Dictionary<string, string> DistinctValues(int shift = 0)
            => Enumerable.Range(1, FieldCount).ToDictionary(
                i => $"field{i}",
                i => new string((char)('a' + (i + shift) % 26), 5 + i));

        private static readonly Lazy<Cassandra.ISession> RawSession = new(
            () => Cassandra.Cluster.Builder().AddContactPoint("localhost").WithPort(9042).Build().Connect());

        private static Task<Dictionary<string, string>> ReadRawRow(string key)
        {
            var cols = string.Join(", ", Enumerable.Range(1, FieldCount).Select(i => $"field{i}"));
            var rs = RawSession.Value.Execute(new Cassandra.SimpleStatement(
                $"SELECT {cols} FROM ycsb.ycsb_table WHERE ycsb_key = ?", key));
            var row = rs.First();

            return Task.FromResult(Enumerable.Range(1, FieldCount)
                .ToDictionary(i => $"field{i}", i => row.GetValue<string>(i - 1)));
        }

        private static Task<(string Key, Dictionary<string, string> Fields)> ReadRawFirstRowFromToken(string startKey)
        {
            var cols = string.Join(", ", Enumerable.Range(1, FieldCount).Select(i => $"field{i}"));
            var rs = RawSession.Value.Execute(new Cassandra.SimpleStatement(
                $"SELECT ycsb_key, {cols} FROM ycsb.ycsb_table WHERE TOKEN(ycsb_key) >= TOKEN(?) LIMIT 1", startKey));
            var row = rs.First();

            var fields = new Dictionary<string, string>();
            for (var i = 1; i <= FieldCount; i++)
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
                Db = "scylladb",
                ThreadCount = 1,
                FieldCount = 10,
                FieldLength = 100,
                ZeroPadding = 1,
                InsertOrder = "hashed",
                Props = [
                    "scylladb.host=localhost",
                    "scylladb.port=9042"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var scyllaClient = new ScyllaYcsbClient(props);

            var scenario = new YcsbScenario(scyllaClient);

            return scenario.Run(settings);
        }
    }
}
