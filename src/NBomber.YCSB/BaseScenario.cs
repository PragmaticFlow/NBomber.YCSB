#pragma warning disable CS4014
using Bogus;
using NBomber.CSharp;
using NBomber.YCSB.DAL;

namespace NBomber.YCSB
{
    public class BaseScenario (IDbYcsbClient dbClient) 
    {
        public void Run(YcsbSettings settings)
        {
            var operations = WorkloadManager.GetOperations(settings.Workload);
            var workloadDescription = WorkloadManager.GetDescription(settings.Workload);

            var scenario = Scenario.Create(workloadDescription, async context =>
            {
                var randomItem = context.Random.Choice(operations);

                switch (randomItem)
                {
                    case "insert":
                        await Step.Run("insert", context, async () =>
                        {
                            var key = context.Random.Next(1, settings.RecordCount + 1).ToString();
                            var values = new Dictionary<string, string> { 
                                ["field_1"] = "value1", 
                                ["field_2"] = "value2" 
                            };
                            return await dbClient.Insert(key, values);
                        });
                        break;

                    case "read":
                        await Step.Run("read", context, async () =>
                        {
                            var key = context.Random.Next(1, settings.RecordCount + 1).ToString();
                            return await dbClient.Read(key);
                        });
                        break;

                    case "read_latest":
                        await Step.Run("read_latest", context, async () =>
                        {
                            return await dbClient.ReadLatest();
                        });
                        break;

                    case "scan":
                        await Step.Run("scan", context, async () =>
                        {
                            var startKey = context.Random.Next(1, settings.RecordCount + 1).ToString();
                            return await dbClient.Scan(startKey, count: 10);
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

            NBomberRunner
               .RegisterScenarios(scenario)
               .WithReportingInterval(TimeSpan.FromSeconds(5))
               //.WithReportFileName(settings.ExportFile)
               .Run();
        }

        public static Dictionary<string, Dictionary<string, string>> GenerateRundoms(int count)
        {
            var faker = new Faker();
            var fields = new[] { "field_1", "field_2" };

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
}
