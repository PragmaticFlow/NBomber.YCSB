#pragma warning disable CS4014
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB;

public class BaseScenario(IDbYcsbClient dbClient) 
{
    public void Run(YcsbCliArgs settings)
    {
        var operations = WorkloadManager.GetOperations(settings.Workload);
        var workloadDescription = WorkloadManager.GetDescription(settings.Workload);
        var tableName = "test_table";

        var gen = new DataGenerator(settings);

        var scenario = Scenario.Create(workloadDescription, async context =>
        {
            var randomItem = context.Random.Choice(operations);

            var values = gen.CreateValues();

            switch (randomItem)
            {
                case OperationType.Insert:
                    await Step.Run("insert", context, async () =>
                    {
                        var key = gen.GetKeyUniform(context);
                        return await dbClient.Insert(table: tableName, key, values);
                    });
                    break;

                case OperationType.Read:
                    await Step.Run("read", context, async () =>
                    {
                        var key = gen.GetKeyZipf(context);
                        return await dbClient.Read(table: tableName, key, null);
                    });
                    break;

                case OperationType.Update:
                    await Step.Run("update", context, async () =>
                    {
                        var key = gen.GetKeyZipf(context);
                        return await dbClient.Update(table: tableName, key, values);
                    });
                    break;

                case OperationType.Scan:
                    await Step.Run("scan", context, async () =>
                    {
                        var key = gen.GetKeyZipf(context);
                        var recordScan = context.Random.Next(1, 10);
                        return await dbClient.Scan(table: tableName, key, recordScan, null);
                    });
                    break; 
            }
            return Response.Ok();
        })
        .WithInit(async context => {
            await dbClient.DeleteAllData();
            dbClient.InitDb();

            var list = gen.GenerateRandoms();

            await dbClient.BulkInsert(tableName, list);
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3));

        var runner = NBomberRunner
               .RegisterScenarios(scenario)
               .WithTestName($"Test {settings.Db} - {workloadDescription}")
               .WithTestSuite($"Test {settings.Db} - {workloadDescription}")
               .WithReportingInterval(TimeSpan.FromSeconds(5));

        if (settings.ExportFile != null)
        {
            runner = runner.WithReportFileName(settings.ExportFile);
        }

        runner.Run();
    }
}
