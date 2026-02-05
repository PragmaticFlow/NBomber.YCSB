using NBomber.YCSB.Infra;
using NBomber.YCSB.Redis;

namespace NBomber.YCSB.Tests
{
    public class WorkloadsTest : IClassFixture<EnvContextFixture>
    {
        [Fact]
        public void WorkloadTest_A_SholdReturn_50Insert_50Read()
        {
            var settings = new YcsbCliArgs
            {
                Workload = Workload.A,
                RecordCount = 1000,
                OperationCount = 1000,
                Db = "redis",
                Copies = 1,
                Duration = 20,
                FieldCount = 10,
                FieldLength = 100,
                ZeroPadding = 1,
                InsertOrder = "hashed",
                Props = ["redis.host=localhost", "redis.port=6379"]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var redisClient = new RedisYcsbClient(props);

            var scenario = new YcsbScenario(redisClient);

            var result = scenario.Run(settings);

            var stats = result.ScenarioStats[0].StepStats;
            
            var insertStep = stats.First(s => s.StepName == "insert");
            var insertCount = insertStep.Ok.Request.Count + insertStep.Fail.Request.Count;

            var readStep = stats.First(s => s.StepName == "read");
            var readCount = readStep.Ok.Request.Count + readStep.Fail.Request.Count;

            var sum = insertCount + readCount;

            var readCountPersent = RoundToNearest(readCount * 100 / sum, 10);
            var insertCountPersent = RoundToNearest(insertCount * 100 / sum, 10);
            
            Assert.Equal(50, readCountPersent);
            Assert.Equal(50, insertCountPersent);
        }

        private static int RoundToNearest(int value, int nearest)
        {
            return (int)Math.Round(value / (double)nearest) * nearest;
        }
    }
}
