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

            switch (operation)
            {
                case OperationType.Insert:
                    await Step.Run("insert", context, () =>
                    {
                        var key = dataGen.GetKeyNext();
                        var values = dataGen.CreateValues();

                        return ycsbClient.Insert(table: tableName, key, values);
                    });
                    break;

                case OperationType.Read:
                    await Step.Run("read", context, () =>
                    {
                        var key = dataGen.GetKeyZipf(context);
                        var fields = dataGen.GetFieldNames();

                        return ycsbClient.Read(table: tableName, key, fields);
                    });
                    break;

                case OperationType.ReadLatest:
                    await Step.Run("read latest", context, () =>
                    {
                        var key = dataGen.GetKeyLatest(context);
                        var fields = dataGen.GetFieldNames();

                        return ycsbClient.Read(table: tableName, key, fields);
                    });
                    break;

                case OperationType.Update:
                    await Step.Run("update", context, () =>
                    {
                        var key = dataGen.GetKeyZipf(context);
                        var values = dataGen.CreateValuesToUpdate();

                        return ycsbClient.Update(table: tableName, key, values);
                    });
                    break;

                case OperationType.Scan:
                    await Step.Run("scan", context, () =>
                    {
                        var key = dataGen.GetKeyZipf(context);
                        var fields = dataGen.GetFieldNames();
                        var recordScan = context.Random.Next(1, 10);

                        return ycsbClient.Scan(table: tableName, key, recordScan, fields);
                    });
                    break;

                case OperationType.ReadModifyWrite:
                    await Step.Run("read-modify-write", context, async () =>
                    {
                        var key = dataGen.GetKeyZipf(context);
                        var fields = dataGen.GetFieldNames();
                        var updateValues = dataGen.CreateValuesToUpdate();

                        return await ycsbClient.ReadModifyWrite(table: tableName, key, fields, updateValues);
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
