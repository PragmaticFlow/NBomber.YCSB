using Aerospike.Client;
using NBomber.YCSB.AerospikeDb;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Tests
{
    public class AerospikeSchemaFixture : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            using var client = AerospikeTests.CreateClient();
            await client.InitDb();
        }

        public Task DisposeAsync()
        {
            AerospikeTests.DisposeRawClient();
            return Task.CompletedTask;
        }
    }

    public class AerospikeTests : IClassFixture<AerospikeSchemaFixture>
    {
        private const int FIELD_COUNT = 10;
        private const string NAMESPACE = "test";
        private const string SET = "ycsb_table";

        private static readonly Lazy<AerospikeClient> RAW_CLIENT = new(() => new AerospikeClient("localhost", 3000));

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
            Assert.Equal(0, readStep.Fail.Request.Count + updateStep.Fail.Request.Count);
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
            Assert.Equal(0, readStep.Fail.Request.Count + updateStep.Fail.Request.Count);
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
            Assert.Equal(0, readStep.Fail.Request.Count);
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
            Assert.Equal(0, insertStep.Fail.Request.Count);
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
            Assert.Equal(0, readStep.Fail.Request.Count + rmwStep.Fail.Request.Count);
        }

        // ---- Field mapping tests ----
        // Stored values are verified through a separate raw Aerospike client.

        [Fact]
        public async Task Insert_Stores_Each_Value_In_Its_Own_Bin()
        {
            using var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();

            var response = await client.Insert("ycsb_table", key, values);

            Assert.False(response.IsError);
            Assert.Equal(values, ReadRawRow(key));
        }

        [Fact]
        public async Task BulkInsert_Stores_Each_Value_In_Its_Own_Bin()
        {
            using var client = CreateClient();
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
            Assert.Equal(values1, ReadRawRow(key1));
            Assert.Equal(values2, ReadRawRow(key2));
        }

        [Fact]
        public async Task BulkInsert_Replaces_Existing_Record()
        {
            using var client = CreateClient();
            var key = NewKey();
            await client.Insert("ycsb_table", key, DistinctValues());

            var fresh = DistinctValues(shift: 3);
            var response = await client.BulkInsert("ycsb_table", new Dictionary<string, Dictionary<string, string>> { [key] = fresh });

            Assert.False(response.IsError);
            Assert.Equal(fresh, ReadRawRow(key));
        }

        [Fact]
        public async Task Read_All_And_Projected_Fields_Report_Matching_Sizes()
        {
            using var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var all = await client.Read("ycsb_table", key, null);
            var one = await client.Read("ycsb_table", key, ["field3"]);

            Assert.Equal(AerospikeHelper.GetSize(key, values), all.SizeBytes);
            Assert.Equal(
                AerospikeHelper.GetSize(key, new Dictionary<string, string> { ["field3"] = values["field3"] }),
                one.SizeBytes);
        }

        [Fact]
        public async Task Read_Missing_Key_Fails_With_No_Data()
        {
            using var client = CreateClient();

            var all = await client.Read("ycsb_table", NewKey(), null);
            var projected = await client.Read("ycsb_table", NewKey(), ["field1"]);

            Assert.True(all.IsError);
            Assert.Equal("no data", all.StatusCode);
            Assert.True(projected.IsError);
            Assert.Equal("no data", projected.StatusCode);
        }

        [Fact]
        public async Task Update_One_Field_Leaves_Other_Fields_Unchanged()
        {
            using var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var response = await client.Update("ycsb_table", key, new Dictionary<string, string> { ["field4"] = "updated" });

            Assert.False(response.IsError);
            var expected = new Dictionary<string, string>(values) { ["field4"] = "updated" };
            Assert.Equal(expected, ReadRawRow(key));
        }

        [Fact]
        public async Task Update_Missing_Key_Fails_And_Creates_No_Record()
        {
            using var client = CreateClient();
            var key = NewKey();

            var response = await client.Update("ycsb_table", key, new Dictionary<string, string> { ["field1"] = "x" });

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);
            Assert.Null(RAW_CLIENT.Value.Get(null, new Key(NAMESPACE, SET, key)));
        }

        [Fact]
        public async Task ReadModifyWrite_Updates_Only_Written_Field_And_Counts_Read_And_Write_Size()
        {
            using var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var write = new Dictionary<string, string> { ["field6"] = "rmw-value" };
            var response = await client.ReadModifyWrite("ycsb_table", key, ["field2"], write);

            Assert.False(response.IsError);
            var readSize = AerospikeHelper.GetSize(key, new Dictionary<string, string> { ["field2"] = values["field2"] });
            Assert.Equal(readSize + AerospikeHelper.GetSize(key, write), response.SizeBytes);

            var expected = new Dictionary<string, string>(values) { ["field6"] = "rmw-value" };
            Assert.Equal(expected, ReadRawRow(key));
        }

        [Fact]
        public async Task ReadModifyWrite_With_All_Fields_Read_Updates_Only_Written_Field()
        {
            using var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var write = new Dictionary<string, string> { ["field1"] = "first", ["field10"] = "last" };
            var response = await client.ReadModifyWrite("ycsb_table", key, null, write);

            Assert.False(response.IsError);
            Assert.Equal(AerospikeHelper.GetSize(key, values) + AerospikeHelper.GetSize(key, write), response.SizeBytes);

            var expected = new Dictionary<string, string>(values) { ["field1"] = "first", ["field10"] = "last" };
            Assert.Equal(expected, ReadRawRow(key));
        }

        [Fact]
        public async Task ReadModifyWrite_Missing_Key_Fails_And_Creates_No_Record()
        {
            using var client = CreateClient();
            var key = NewKey();

            var response = await client.ReadModifyWrite("ycsb_table", key, null, new Dictionary<string, string> { ["field1"] = "x" });

            Assert.True(response.IsError);
            Assert.Equal("no data", response.StatusCode);
            Assert.Null(RAW_CLIENT.Value.Get(null, new Key(NAMESPACE, SET, key)));
        }

        [Fact]
        public async Task Insert_Duplicate_Key_Throws_And_Keeps_Original_Record()
        {
            using var client = CreateClient();
            var key = NewKey();
            var values = DistinctValues();
            await client.Insert("ycsb_table", key, values);

            var ex = await Assert.ThrowsAsync<AerospikeException>(
                () => client.Insert("ycsb_table", key, DistinctValues(shift: 7)));

            Assert.Equal(ResultCode.KEY_EXISTS_ERROR, ex.Result);
            Assert.Equal(values, ReadRawRow(key));
        }

        [Fact]
        public async Task Scan_Is_Not_Supported_And_Reads_Nothing()
        {
            using var client = CreateClient();
            var key = NewKey();
            await client.Insert("ycsb_table", key, DistinctValues());

            var all = await client.Scan("ycsb_table", key, 10, null);
            var projected = await client.Scan("ycsb_table", key, 10, ["field1"]);

            Assert.True(all.IsError);
            Assert.Equal("scan not supported", all.StatusCode);
            Assert.True(projected.IsError);
            Assert.Equal("scan not supported", projected.StatusCode);
        }

        // ---- Reset tests ----

        [Fact]
        public async Task DeleteAllData_Then_InitDb_Gives_Empty_Set_That_Accepts_Writes()
        {
            var set = NewSet();
            using var client = CreateClient(set);
            await client.Insert("ycsb_table", NewKey(), DistinctValues());
            await client.Insert("ycsb_table", NewKey(), DistinctValues());

            try
            {
                var response = await client.DeleteAllData();
                Assert.False((await client.InitDb()).IsError);

                Assert.False(response.IsError);
                await WaitForCount(set, 0);

                var key = NewKey();
                var values = DistinctValues();
                Assert.False((await client.Insert("ycsb_table", key, values)).IsError);
                Assert.Equal(values, ReadRawRow(key, set));
            }
            finally
            {
                Truncate(set);
            }
        }

        [Fact]
        public async Task Writes_Right_After_DeleteAllData_Survive_Repeated_Resets()
        {
            var set = NewSet();
            using var client = CreateClient(set);

            try
            {
                for (var round = 0; round < 20; round++)
                {
                    await client.DeleteAllData();
                    await client.InitDb();

                    var keys = Enumerable.Range(0, 20).Select(_ => NewKey()).ToList();
                    await client.BulkInsert("ycsb_table", keys.ToDictionary(k => k, _ => DistinctValues()));

                    foreach (var key in keys)
                        Assert.False((await client.Read("ycsb_table", key, null)).IsError);
                }
            }
            finally
            {
                Truncate(set);
            }
        }

        [Fact]
        public async Task DeleteAllData_Keeps_Other_Sets_In_Namespace()
        {
            var otherSet = NewSet();
            var key = NewKey();
            var values = DistinctValues();
            using var other = CreateClient(otherSet);
            await other.Insert("ycsb_table", key, values);

            try
            {
                using var fresh = CreateClient(NewSet());
                var response = await fresh.DeleteAllData();

                Assert.False(response.IsError);
                Assert.Equal(values, ReadRawRow(key, otherSet));
            }
            finally
            {
                Truncate(otherSet);
            }
        }

        [Fact]
        public async Task DeleteAllData_On_Never_Used_Set_Succeeds_Creates_Nothing_And_InitDb_Then_Works()
        {
            var set = NewSet();
            using var client = CreateClient(set);

            try
            {
                var response = await client.DeleteAllData();

                Assert.False(response.IsError);
                Assert.Equal(0, CountRecords(set));

                Assert.False((await client.InitDb()).IsError);
                var key = NewKey();
                Assert.False((await client.Insert("ycsb_table", key, DistinctValues())).IsError);
                Assert.False((await client.Read("ycsb_table", key, null)).IsError);
            }
            finally
            {
                Truncate(set);
            }
        }

        [Fact]
        public async Task InitDb_With_Unknown_Namespace_Fails()
        {
            using var client = CreateClient(ns: $"missing_{Guid.NewGuid():N}"[..20]);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.InitDb());

            Assert.Contains("does not exist", ex.Message);
        }

        [Fact]
        public async Task DeleteAllData_With_Unknown_Namespace_Fails_With_Clear_Error()
        {
            using var client = CreateClient(ns: $"missing_{Guid.NewGuid():N}"[..20]);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeleteAllData());

            Assert.Contains("does not exist", ex.Message);
        }

        [Fact]
        public async Task More_Than_512_Concurrent_Operations_Complete()
        {
            var set = NewSet();
            using var client = CreateClient(set);
            var keys = Enumerable.Range(0, 2000).Select(_ => NewKey()).ToList();

            try
            {
                var response = await client.BulkInsert("ycsb_table", keys.ToDictionary(k => k, _ => DistinctValues()));
                Assert.False(response.IsError);

                var reads = await Task.WhenAll(keys.Select(k => client.Read("ycsb_table", k, null)));

                Assert.All(reads, r => Assert.False(r.IsError));
            }
            finally
            {
                Truncate(set);
            }
        }

        [Fact]
        public void Unreachable_Server_Throws()
        {
            Assert.ThrowsAny<AerospikeException>(() => new AerospikeYcsbClient(YcsbCliArgs.ParseProps([
                "aerospike.host=localhost",
                "aerospike.port=3999",
                "aerospike.timeout=1000"
            ])));
        }

        internal static AerospikeYcsbClient CreateClient(string set = SET, string ns = NAMESPACE)
            => new(YcsbCliArgs.ParseProps([
                "aerospike.host=localhost",
                "aerospike.port=3000",
                $"aerospike.namespace={ns}",
                $"aerospike.set={set}"
            ]));

        private static string NewKey() => $"aerospike-test-{Guid.NewGuid():N}";

        private static string NewSet() => $"s_{Guid.NewGuid():N}"[..12];

        // field{i} gets a value with a distinct character and length, so a shifted bin is detectable.
        private static Dictionary<string, string> DistinctValues(int shift = 0)
            => Enumerable.Range(1, FIELD_COUNT).ToDictionary(
                i => $"field{i}",
                i => new string((char)('a' + (i + shift) % 26), 5 + i));

        private static Dictionary<string, string> ReadRawRow(string key, string set = SET)
        {
            var record = RAW_CLIENT.Value.Get(null, new Key(NAMESPACE, set, key));
            Assert.NotNull(record);

            return record.bins.ToDictionary(b => b.Key, b => (string)b.Value);
        }

        private static int CountRecords(string set)
        {
            var count = 0;
            RAW_CLIENT.Value.ScanAll(null, NAMESPACE, set, (_, _) => Interlocked.Increment(ref count));
            return count;
        }

        // Truncate is asynchronous on the server, so the set may take a moment to read as empty.
        private static async Task WaitForCount(string set, int expected)
        {
            for (var i = 0; i < 50 && CountRecords(set) != expected; i++)
                await Task.Delay(100);

            Assert.Equal(expected, CountRecords(set));
        }

        internal static void DisposeRawClient()
        {
            if (RAW_CLIENT.IsValueCreated) RAW_CLIENT.Value.Close();
        }

        private static void Truncate(string set) => RAW_CLIENT.Value.Truncate(null, NAMESPACE, set, null);

        private Contracts.Stats.NodeStats RunWorkload(Workload workload)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = 1000,
                OperationCount = 1000,
                Db = "aerospike",
                ThreadCount = 1,
                FieldCount = 10,
                FieldLength = 100,
                ZeroPadding = 1,
                InsertOrder = "hashed",
                Props = [
                    "aerospike.host=localhost",
                    "aerospike.port=3000"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            using var aerospikeClient = new AerospikeYcsbClient(props);

            var scenario = new YcsbScenario(aerospikeClient);

            return scenario.Run(settings);
        }
    }
}
