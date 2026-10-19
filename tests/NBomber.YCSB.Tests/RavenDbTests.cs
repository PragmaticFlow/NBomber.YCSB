using NBomber.YCSB.Infra;
using NBomber.YCSB.RavenDb;

namespace NBomber.YCSB.Tests
{
    public class RavenDbTests
    {
        [Fact]
        public async Task Workload_A_Shold_Execute_50_Read_50_Update()
        {
            await TestsHelper.RetryAsync(() =>
            {
                var result = RunWorkload(Workload.A, recordCount: 1000, operationCount: 200);

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
                var result = RunWorkload(Workload.B, recordCount: 1000, operationCount: 1000);

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
                var result = RunWorkload(Workload.C, recordCount: 1000, operationCount: 1000);

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
                var result = RunWorkload(Workload.D, recordCount: 1000, operationCount: 1000);

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
                var result = RunWorkload(Workload.E, recordCount: 1000, operationCount: 1000);

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
                var result = RunWorkload(Workload.F, recordCount: 1000, operationCount: 200);

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

        private Contracts.Stats.NodeStats RunWorkload(Workload workload, ulong recordCount, int operationCount)
        {
            var settings = new YcsbCliArgs
            {
                Workload = workload,
                RecordCount = recordCount,
                OperationCount = operationCount,
                Db = "ravendb",
                ThreadCount = 1,
                FieldCount = 10,
                FieldLength = 100,
                ZeroPadding = 5,
                InsertOrder = "ordered",
                ReadAllFields = false,
                Props = [
                    "ravendb.url=http://localhost:8080",
                    "ravendb.database=ycsb"
                ]
            };

            var props = YcsbCliArgs.ParseProps(settings.Props);

            var ravenDbClient = new RavenDbYcsbClient(props);

            var scenario = new YcsbScenario(ravenDbClient);

            return scenario.Run(settings);
        }
    }
}
