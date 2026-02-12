using NBomber.YCSB.Infra;
using NBomber.YCSB.MongoDb;

namespace NBomber.YCSB.Tests
{
    public class MongoDbTests
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
                Props = ["mongodb.url=mongodb://localhost:27017"]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var mongoClient = new MongoDbYcsbClient(props);

            var scenario = new YcsbScenario(mongoClient);

            return scenario.Run(settings);
        }
    }
}
