using NBomber.YCSB.Infra;
using NBomber.YCSB.KeyDb;
using StackExchange.Redis;

namespace NBomber.YCSB.Tests
{
    public class KeyDbTests
    {
        private const string ENDPOINT = "localhost:6380";

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

        // Live KeyDB tests against a dedicated container: Workload_* and DeleteAllData_* flush it, the rest use unique keys.
        [Fact]
        public async Task Insert_Then_Read_Should_Use_Same_Key_And_Field_Names()
        {
            using var client = CreateClient();
            var key = $"test-{Guid.NewGuid()}";
            var values = new Dictionary<string, string> { ["field0"] = "a", ["field1"] = "b" };

            var insert = await client.Insert("usertable", key, values);
            var readAll = await client.Read("usertable", key, new HashSet<string>());
            var readFields = await client.Read("usertable", key, new HashSet<string> { "field0" });
            var readMissingFields = await client.Read("usertable", key, new HashSet<string> { "nope" });

            Assert.False(insert.IsError);
            Assert.False(readAll.IsError);
            Assert.False(readFields.IsError);
            Assert.True(readMissingFields.IsError);
        }

        [Fact]
        public async Task Read_And_ReadModifyWrite_With_Fields_Should_Fail_For_Missing_Key()
        {
            using var client = CreateClient();
            var key = $"missing-{Guid.NewGuid()}";
            var fields = new HashSet<string> { "field0" };

            var read = await client.Read("usertable", key, fields);
            var rmw = await client.ReadModifyWrite("usertable", key, fields, new Dictionary<string, string> { ["field0"] = "x" });

            Assert.True(read.IsError);
            Assert.True(rmw.IsError);
        }

        [Fact]
        public async Task Scan_With_Fields_Should_Return_Only_Requested_Fields_Size()
        {
            using var client = CreateClient();
            var prefix = $"scan-{Guid.NewGuid()}";
            var values = new Dictionary<string, string> { ["field0"] = "aaaa", ["field1"] = new string('b', 1000) };

            await client.BulkInsert("usertable", new Dictionary<string, Dictionary<string, string>> { [prefix] = values });

            var all = await client.Scan("usertable", prefix, 1, new HashSet<string>());
            var subset = await client.Scan("usertable", prefix, 1, new HashSet<string> { "field0" });

            Assert.False(subset.IsError);
            Assert.True(subset.SizeBytes < all.SizeBytes);
        }

        [Fact]
        public async Task Tests_Should_Hit_KeyDb_With_Two_Server_Threads()
        {
            using var mux = await ConnectionMultiplexer.ConnectAsync($"{ENDPOINT},allowAdmin=true");
            var server = mux.GetServer(mux.GetEndPoints()[0]);

            var info = (await server.InfoAsync()).SelectMany(g => g).ToDictionary(kv => kv.Key, kv => kv.Value);

            Assert.Contains("keydb", info["executable"], StringComparison.OrdinalIgnoreCase);
            Assert.Equal("2", info["server_threads"]);
        }

        [Fact]
        public async Task Index_Sorted_Set_Should_Support_Lexicographic_Range_Inclusive_And_Ordered()
        {
            using var client = CreateClient();
            var prefix = $"lex-{Guid.NewGuid()}";
            var values = new Dictionary<string, string> { ["field0"] = "v" };
            var data = new Dictionary<string, Dictionary<string, string>>
            {
                [$"{prefix}-c"] = values,
                [$"{prefix}-a"] = values,
                [$"{prefix}-b"] = values
            };

            await client.BulkInsert("usertable", data);

            using var mux = await ConnectionMultiplexer.ConnectAsync(ENDPOINT);
            var keys = await mux.GetDatabase().SortedSetRangeByValueAsync("_indices", $"{prefix}-b", default, Exclude.None, Order.Ascending, 0, 2);
            var scan = await client.Scan("usertable", $"{prefix}-b", 2, new HashSet<string>());

            Assert.Equal([$"{prefix}-b", $"{prefix}-c"], keys.Select(k => (string)k!).ToArray());
            Assert.False(scan.IsError);
            Assert.True(scan.SizeBytes > 0);
        }

        [Fact]
        public async Task DeleteAllData_Should_Flush_Keys_And_Index()
        {
            using var client = CreateClient();
            var key = $"flush-{Guid.NewGuid()}";
            await client.Insert("usertable", key, new Dictionary<string, string> { ["field0"] = "v" });

            var delete = await client.DeleteAllData();
            var read = await client.Read("usertable", key, new HashSet<string>());

            using var mux = await ConnectionMultiplexer.ConnectAsync(ENDPOINT);
            Assert.False(delete.IsError);
            Assert.True(read.IsError);
            Assert.False(await mux.GetDatabase().KeyExistsAsync("_indices"));
        }

        [Fact]
        public async Task Update_Should_Preserve_Unrelated_Fields()
        {
            using var client = CreateClient();
            var key = $"upd-{Guid.NewGuid()}";
            await client.Insert("usertable", key, new Dictionary<string, string> { ["field0"] = "a", ["field1"] = "b" });

            await client.Update("usertable", key, new Dictionary<string, string> { ["field0"] = "z" });

            using var mux = await ConnectionMultiplexer.ConnectAsync(ENDPOINT);
            var hash = await mux.GetDatabase().HashGetAllAsync($"hash:{key}");
            Assert.Equal("z", hash.First(h => h.Name == "field0").Value);
            Assert.Equal("b", hash.First(h => h.Name == "field1").Value);
        }

        [Fact]
        public async Task Unreachable_Server_Should_Throw()
        {
            using var client = new KeyDbYcsbClient(YcsbCliArgs.ParseProps(["keydb.host=localhost", "keydb.port=1"]));

            await Assert.ThrowsAnyAsync<Exception>(() => client.DeleteAllData());
        }

        [Fact]
        public async Task Client_Should_Default_To_Port_6380_With_KeyDb_Prefix()
        {
            using var client = new KeyDbYcsbClient(YcsbCliArgs.ParseProps(["keydb.host=localhost"]));
            var key = $"default-port-{Guid.NewGuid()}";

            await client.Insert("usertable", key, new Dictionary<string, string> { ["field0"] = "v" });

            using var mux = await ConnectionMultiplexer.ConnectAsync(ENDPOINT);
            var db = mux.GetDatabase();
            var exists = await db.KeyExistsAsync($"hash:{key}");
            await db.KeyDeleteAsync($"hash:{key}");
            await db.SortedSetRemoveAsync("_indices", key);

            Assert.True(exists);
        }

        [Fact]
        public async Task Client_Should_Ignore_Redis_Props()
        {
            using var client = new KeyDbYcsbClient(YcsbCliArgs.ParseProps(["redis.host=invalid.invalid", "redis.port=1"]));
            var key = $"ignore-redis-props-{Guid.NewGuid()}";

            var insert = await client.Insert("usertable", key, new Dictionary<string, string> { ["field0"] = "v" });

            using var mux = await ConnectionMultiplexer.ConnectAsync(ENDPOINT);
            var db = mux.GetDatabase();
            await db.KeyDeleteAsync($"hash:{key}");
            await db.SortedSetRemoveAsync("_indices", key);

            Assert.False(insert.IsError);
        }

        private static KeyDbYcsbClient CreateClient()
        {
            return new KeyDbYcsbClient(YcsbCliArgs.ParseProps(["keydb.host=localhost", "keydb.port=6380"]));
        }

        private Contracts.Stats.NodeStats RunWorkload(Workload workload)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = 1000,
                OperationCount = 1000,
                Db = "keydb",
                ThreadCount = 1,
                FieldCount = 10,
                FieldLength = 100,
                ZeroPadding = 1,
                InsertOrder = "hashed",
                Props = ["keydb.host=localhost", "keydb.port=6380"]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            using var keyDbClient = new KeyDbYcsbClient(props);

            var scenario = new YcsbScenario(keyDbClient);

            return scenario.Run(settings);
        }
    }
}
