#pragma warning disable CS4014
using Bogus;
using NBomber.Contracts;
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

        var scenario = Scenario.Create(workloadDescription, async context =>
        {
            var randomItem = context.Random.Choice(operations);
            
            var data = context.ScenarioInstanceData;

            var values = new Dictionary<string, string>
            {
                ["1"] = "value1",
                ["2"] = "value2"
            };

            switch (randomItem)
            {
                case OperationType.Insert:
                    await Step.Run("insert", context, async () =>
                    {
                        var key = GetRundomUniform(settings.RecordCount, context);
                        return await dbClient.Insert(table: "", key, values);
                    });
                    break;

                case OperationType.Read:
                    await Step.Run("read", context, async () =>
                    {
                        var key = GetRundomZipf(settings.RecordCount, context);
                        var columns = new HashSet<string> { "1" };
                        return await dbClient.Read(table: "", key, columns);
                    });
                    break;

                case OperationType.Update:
                    await Step.Run("update", context, async () =>
                    {
                        var key = GetRundomZipf(settings.RecordCount, context);
                        return await dbClient.Update(table: "", key, values);
                    });
                    break;

                case OperationType.Scan:
                    await Step.Run("scan", context, async () =>
                    {
                        var key = GetRundomZipf(settings.RecordCount, context);
                        var recordScan = context.Random.Next(1, 10);
                        var columns = new HashSet<string> { "1" };
                        return await dbClient.Scan(table: "", key, recordScan, columns);
                    });
                    break; 
            }
            return Response.Ok();
        })
        .WithInit(async context => {
            await dbClient.DeleteAllData();
            dbClient.InitDb();
            var list = GenerateRundoms(settings.RecordCount);
            await dbClient.BulkInsert(list);
        })
        .WithWarmUpDuration(TimeSpan.FromSeconds(3));

        var runner = NBomberRunner
               .RegisterScenarios(scenario)
               .WithReportingInterval(TimeSpan.FromSeconds(5));

        if (settings.ExportFile != null)
        {
            runner = runner.WithReportFileName(settings.ExportFile);
        }

        runner.Run();
    }

    public static string GetRundomZipf(int recordCount, IScenarioContext context)
    {
        return context.Random.Zipf(recordCount, 1.3).ToString();
    }

    public static string GetRundomUniform(int recordCount, IScenarioContext context)
    {
        return context.Random.Next(1, recordCount + 1).ToString();
    }
    public static Dictionary<string, Dictionary<string, string>> GenerateRundoms(int count)
    {
        var faker = new Faker();
        var fields = new[] { "1", "2" };

        var keys = Enumerable.Range(1, count).Select(i => i.ToString());

        return keys.ToDictionary(
            key => key,
            key => fields.ToDictionary(
                field => field,
                field => faker.Lorem.Word()
            )
        );
    }
}
