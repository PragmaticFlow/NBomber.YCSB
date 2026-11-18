#pragma warning disable CS4014
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB;

public class YcsbScenario(IDbYcsbClient ycsbClient) 
{
    public Contracts.Stats.NodeStats Run(YcsbCliArgs settings)
    {
        var operations = WorkloadManager.GetOperations(settings.Workload);
        var workloadDescription = WorkloadManager.GetDescription(settings.Workload);
        var tableName = "test_table";

        var insertProportion = (double)operations.Where(o => o.Item1 == OperationType.Insert).Sum(o => o.Item2)
                               / operations.Sum(o => o.Item2);
        var dataGen = new DataGenerator(settings, insertProportion);

        var scenario = Scenario.Create(workloadDescription, async context =>
        {
            var operation = context.Random.Choice(operations); 

            switch (operation)
            {
                case OperationType.Insert:
                {
                    var values = dataGen.CreateValues();
                    var (keyNum, key) = dataGen.GetKeyNext();

                    try
                    {
                        await Step.Run("insert", context, () => ycsbClient.Insert(table: tableName, key, values));
                    }
                    finally
                    {
                        dataGen.AcknowledgeInsert(keyNum);
                    }
                    break;
                }

                case OperationType.Read:
                {
                    var key = dataGen.GetKeyZipf(context.Random);
                    var fields = dataGen.GetFieldNames();

                    await Step.Run("read", context, () => ycsbClient.Read(table: tableName, key, fields));
                    break;
                }

                case OperationType.ReadLatest:
                {
                    var key = dataGen.GetKeyLatest(context.Random);
                    var fields = dataGen.GetFieldNames();

                    await Step.Run("read latest", context, () => ycsbClient.Read(table: tableName, key, fields));
                    break;
                }

                case OperationType.Update:
                {
                    var key = dataGen.GetKeyZipf(context.Random);
                    var values = dataGen.CreateValuesToUpdate();

                    await Step.Run("update", context, () => ycsbClient.Update(table: tableName, key, values));
                    break;
                }

                case OperationType.Scan:
                {
                    var key = dataGen.GetKeyZipf(context.Random);
                    var fields = dataGen.GetFieldNames();
                    var recordScan = dataGen.NextScanLength(context.Random);

                    await Step.Run("scan", context, () => ycsbClient.Scan(table: tableName, key, recordScan, fields));
                    break;
                }

                case OperationType.ReadModifyWrite:
                {
                    var key = dataGen.GetKeyZipf(context.Random);
                    var fields = dataGen.GetFieldNames();
                    var updateValues = dataGen.CreateValuesToUpdate();

                    await Step.Run("read-modify-write", context, () => ycsbClient.ReadModifyWrite(table: tableName, key, fields, updateValues));
                    break;
                }
            }
            return Response.Ok();
        })
        .WithInit(async context =>
        {
            await ycsbClient.DeleteAllData();
            await ycsbClient.InitDb();

            var list = dataGen.GenerateRandoms();

            await ycsbClient.BulkInsert(tableName, list);
        })
        .WithLoadSimulations(
            Simulation.IterationsForConstant(copies: settings.ThreadCount, iterations: settings.OperationCount)
        );

        scenario = settings.WarmUp > 0
            ? scenario.WithWarmUpDuration(TimeSpan.FromSeconds(settings.WarmUp))
            : scenario.WithoutWarmUp();

        var runner = NBomberRunner
               .RegisterScenarios(scenario)
               .WithTestName($"Test {settings.Db} - {workloadDescription}")
               .WithTestSuite($"Test {settings.Db} - {workloadDescription}")
               .WithReportingInterval(TimeSpan.FromSeconds(5));

        if (!string.IsNullOrWhiteSpace(settings.ExportFile))
        {
            var folder = Path.GetDirectoryName(settings.ExportFile);
            if (!string.IsNullOrEmpty(folder))
                runner = runner.WithReportFolder(folder);

            runner = runner.WithReportFileName(Path.GetFileName(settings.ExportFile));
        }

        return runner.Run();
    }
}
