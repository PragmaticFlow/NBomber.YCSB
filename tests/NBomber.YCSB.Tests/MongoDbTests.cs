using MongoDB.Bson;
using MongoDB.Driver;
using NBomber.YCSB.Infra;
using NBomber.YCSB.MongoDb;

namespace NBomber.YCSB.Tests
{
    public class MongoDbTests
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
        public async Task Scan_Should_Read_Up_To_Count_Records_In_Id_Order_From_Start_Key()
        {
            await WithTable(async (client, table, col) =>
            {
                var docs = await SeedTen(client, table);

                var response = await client.Scan(table, "k03", 5, []);

                Assert.False(response.IsError);
                Assert.Equal(docs.Skip(2).Take(5).Sum(MongoDbHelper.GetSize), response.SizeBytes);

                // near the end of the key range only the two remaining records are read
                var tail = await client.Scan(table, "k09", 5, []);

                Assert.False(tail.IsError);
                Assert.Equal(docs.Skip(8).Sum(MongoDbHelper.GetSize), tail.SizeBytes);

                var empty = await client.Scan(table, "k99", 5, []);

                Assert.True(empty.IsError);
                Assert.Equal("no data", empty.StatusCode);
            });
        }

        [Fact]
        public async Task Read_Should_Return_Only_Requested_Field()
        {
            await WithTable(async (client, table, col) =>
            {
                var docs = await SeedTen(client, table);

                var response = await client.Read(table, "k01", ["field3"]);

                var expected = new BsonDocument { ["_id"] = "k01", ["field3"] = docs[0]["field3"] };

                Assert.False(response.IsError);
                Assert.Equal(MongoDbHelper.GetSize(expected), response.SizeBytes);
            });
        }

        [Fact]
        public async Task Missing_Key_Should_Return_No_Data()
        {
            await WithTable(async (client, table, col) =>
            {
                await SeedTen(client, table);

                var read = await client.Read(table, "missing", null);
                var rmw = await client.ReadModifyWrite(table, "missing", null, new Dictionary<string, string> { ["field1"] = "x" });
                var update = await client.Update(table, "missing", new Dictionary<string, string> { ["field1"] = "x" });

                foreach (var response in new[] { read, rmw, update })
                {
                    Assert.True(response.IsError);
                    Assert.Equal("no data", response.StatusCode);
                }
            });
        }

        [Fact]
        public async Task Update_Should_Preserve_Other_Fields_And_Add_No_Timestamps()
        {
            await WithTable(async (client, table, col) =>
            {
                await SeedTen(client, table);

                var update = await client.Update(table, "k01", new Dictionary<string, string> { ["field2"] = "new" });
                var rmw = await client.ReadModifyWrite(table, "k02", ["field1"], new Dictionary<string, string> { ["field2"] = "new" });

                Assert.False(update.IsError);
                Assert.False(rmw.IsError);

                foreach (var key in new[] { "k01", "k02" })
                {
                    var doc = await col.Find(MongoDbHelper.BuildFilter(key)).FirstAsync();

                    Assert.Equal(new[] { "_id", "field1", "field2", "field3" }, doc.Names.ToArray());
                    Assert.Equal("new", doc["field2"].AsString);
                    Assert.Equal($"{key}-field1", doc["field1"].AsString);
                    Assert.Equal($"{key}-field3", doc["field3"].AsString);
                }
            });
        }

        [Fact]
        public async Task DeleteAllData_Should_Drop_Only_Ycsb_Database()
        {
            var props = YcsbCliArgs.ParseProps(["mongodb.host=localhost", "mongodb.port=27017"]);
            var client = new MongoDbYcsbClient(props);

            var mongo = new MongoClient("mongodb://localhost:27017");
            var otherName = $"ycsb_other_{Guid.NewGuid():N}";
            var other = mongo.GetDatabase(otherName);

            try
            {
                await other.GetCollection<BsonDocument>("data").InsertOneAsync(new BsonDocument { ["_id"] = "k01" });
                await mongo.GetDatabase("ycsb").GetCollection<BsonDocument>("ycsb_reset_test").InsertOneAsync(new BsonDocument { ["_id"] = "k01" });

                var response = await client.DeleteAllData();

                Assert.False(response.IsError);
                Assert.Equal(1, await other.GetCollection<BsonDocument>("data").CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
                Assert.Empty(await (await mongo.GetDatabase("ycsb").ListCollectionNamesAsync()).ToListAsync());
            }
            finally
            {
                await mongo.DropDatabaseAsync(otherName);
            }
        }

        private static async Task WithTable(Func<MongoDbYcsbClient, string, IMongoCollection<BsonDocument>, Task> test)
        {
            var props = YcsbCliArgs.ParseProps(["mongodb.host=localhost", "mongodb.port=27017"]);
            var client = new MongoDbYcsbClient(props);

            // unique collection per test, so only own data is touched
            var table = $"ycsb_test_{Guid.NewGuid():N}";
            var db = new MongoClient("mongodb://localhost:27017").GetDatabase("ycsb");

            try
            {
                await test(client, table, db.GetCollection<BsonDocument>(table));
            }
            finally
            {
                await db.DropCollectionAsync(table);
            }
        }

        private static async Task<List<BsonDocument>> SeedTen(MongoDbYcsbClient client, string table)
        {
            var docs = new List<BsonDocument>();

            for (var i = 1; i <= 10; i++)
            {
                var key = $"k{i:00}";
                var values = new Dictionary<string, string>
                {
                    ["field1"] = $"{key}-field1",
                    ["field2"] = $"{key}-field2",
                    ["field3"] = $"{key}-field3"
                };

                await client.Insert(table, key, values);
                docs.Add(MongoDbHelper.BuildDocument(key, values));
            }

            return docs;
        }

        private Contracts.Stats.NodeStats RunWorkload(Workload workload)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = 1000,
                OperationCount = 1000,
                Db = "mongodb",
                ThreadCount = 1,
                FieldCount = 5,
                FieldLength = 100,
                ZeroPadding = 5,
                InsertOrder = "ordered",
                ReadAllFields = false,
                Props = [
                    "mongodb.host=localhost",
                    "mongodb.port=27017"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var mongoClient = new MongoDbYcsbClient(props);

            var scenario = new YcsbScenario(mongoClient);

            return scenario.Run(settings);
        }
    }
}
