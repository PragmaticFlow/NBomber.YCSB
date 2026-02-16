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

        var dataGen = new DataGenerator(settings);

        var scenario = Scenario.Create(workloadDescription, async context =>
        {
            var operation = context.Random.Choice(operations);
            var values = dataGen.CreateValues();

            switch (operation)
            {
                case OperationType.Insert:
                    await Step.Run("insert", context, async () =>
                    {
                        var key = dataGen.GetKeyNext();
                        return await ycsbClient.Insert(table: tableName, key, values);
                    });
                    break;

                case OperationType.Read:
                    await Step.Run("read", context, async () =>
                    {
                        var key = dataGen.GetKeyZipf(context);
                        var fields = dataGen.GetFieldNames();

                        return await ycsbClient.Read(table: tableName, key, fields);
                    });
                    break;

                case OperationType.ReadLatest:
                    await Step.Run("read latest", context, async () =>
                    {
                        var key = dataGen.GetKeyLatest(context);
                        var fields = dataGen.GetFieldNames();

                        return await ycsbClient.Read(table: tableName, key, fields);
                    });
                    break;

                case OperationType.Update:
                    await Step.Run("update", context, async () =>
                    {
                        var key = dataGen.GetKeyZipf(context);
                        return await ycsbClient.Update(table: tableName, key, values);
                    });
                    break;

                case OperationType.Scan:
                    await Step.Run("scan", context, async () =>
                    {
                        var key = dataGen.GetKeyZipf(context);
                        var fields = dataGen.GetFieldNames();
                        var recordScan = context.Random.Next(1, 10);

                        return await ycsbClient.Scan(table: tableName, key, recordScan, fields);
                    });
                    break;
            }
            return Response.Ok();
        })
        .WithInit(async context =>
        {
            await ycsbClient.InitDb();
            await ycsbClient.DeleteAllData();

            var list = dataGen.GenerateRandoms();

            await ycsbClient.BulkInsert(tableName, list);

            dataGen.SetRecordCount(settings.RecordCount);
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3))
        .WithLoadSimulations(
            Simulation.IterationsForConstant(copies: settings.ThreadCount, iterations: settings.OperationCount)
        );

        var runner = NBomberRunner
               .RegisterScenarios(scenario)
               .WithTestName($"Test {settings.Db} - {workloadDescription}")
               .WithTestSuite($"Test {settings.Db} - {workloadDescription}")
               .WithReportingInterval(TimeSpan.FromSeconds(5));

        if (settings.ExportFile != null)
        {
            runner = runner.WithReportFileName(settings.ExportFile);
        }

        return runner.Run();
    }
}
