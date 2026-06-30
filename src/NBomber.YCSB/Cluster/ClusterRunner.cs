using NBomber.Contracts.Stats;
using NBomber.CSharp;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Cluster;

public static class ClusterRunner
{
    public static void Run(ClusterCliArgs settings)
    {
        var props = YcsbCliArgs.ParseProps(settings.Props);

        var databases = ParseDatabases(settings.Db);

        var scenarios = databases
            .Select(db => YcsbScenario.BuildScenario(
                scenarioName: db,
                settings: settings,
                ycsbClient: DbClientFactory.Create(db, props)))
            .ToArray();

        Console.WriteLine($"Starting YCSB cluster node. Config: '{settings.Config}'. " +
                          $"Scenarios: {string.Join(", ", databases)}. " +
                          $"Workload: {settings.Workload} ({WorkloadManager.GetDescription(settings.Workload)}).");

        var runner = NBomberRunner
            .RegisterScenarios(scenarios)
            .LoadConfig(settings.Config);

        if (settings.AgentsCount > 0)
            runner = runner.WithAgentsCount(settings.AgentsCount);

        if (!string.IsNullOrWhiteSpace(settings.License))
            runner = runner.WithLicense(settings.License);
        else if (settings.LocalDev)
            runner = runner.EnableLocalDevCluster(true);
        
        runner.Run();
    }

    private static string[] ParseDatabases(string? db)
    {
        if (string.IsNullOrWhiteSpace(db))
            return DbClientFactory.SupportedDatabases;

        var requested = db
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.ToLower())
            .Distinct()
            .ToArray();

        var unknown = requested.Except(DbClientFactory.SupportedDatabases).ToArray();
        if (unknown.Length > 0)
            throw new NotSupportedException(
                $"Unsupported database(s): {string.Join(", ", unknown)}. " +
                $"Supported: {string.Join(", ", DbClientFactory.SupportedDatabases)}.");

        return requested;
    }
}
