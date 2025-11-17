#pragma warning disable CS4014
using Bogus;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB
{
    public class BaseScenario (IDbYcsbClient dbClient) 
    {
        public void Run(YcsbCliArgs settings)
        {
            var operations = WorkloadManager.GetOperations(settings.Workload);
            var workloadDescription = WorkloadManager.GetDescription(settings.Workload);

            var scenario = Scenario.Create(workloadDescription, async context =>
            {
                var randomItem = context.Random.Choice(operations);
                
                //var data = context.ScenarioInstanceData;

                var values = new Dictionary<string, string>
                {
                    ["field_1"] = "value1",
                    ["field_2"] = "value2"
                };

                switch (randomItem)
                {
                    case "insert":
                        await Step.Run("insert", context, async () =>
                        {
                            var key = context.Random.Next(1, settings.RecordCount + 1).ToString();
                            return await dbClient.Insert(table: "", key, values);
                        });
                        break;

                    case "read":
                        await Step.Run("read", context, async () =>
                        {
                            var key = context.Random.Zipf(settings.RecordCount, 1.3).ToString();
                            var columns = new HashSet<string>();
                            return await dbClient.Read(table: "", key, columns);
                        });
                        break;

                    case "update":
                        await Step.Run("update", context, async () =>
                        {
                            var key = context.Random.Zipf(settings.RecordCount, 1.3).ToString();
                            return await dbClient.Update(table: "", key, values);
                        });
                        break;

                    case "scan":
                        await Step.Run("scan", context, async () =>
                        {
                            var key = context.Random.Zipf(settings.RecordCount, 1.3).ToString();
                            var recordScan = context.Random.Next(1, 10);
                            var columns = new HashSet<string>();
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
