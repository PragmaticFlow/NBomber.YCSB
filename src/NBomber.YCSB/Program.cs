using CommandLine;
using NBomber.YCSB;
using NBomber.YCSB.Cluster;
using NBomber.YCSB.Infra;

Console.WriteLine("NBomber YCSB interactive console started.");
Console.WriteLine("Type a command (for example):");
Console.WriteLine("run --workload A --recordcount 1000 --db redis -p redis.host=localhost -p redis.port=6379");
Console.WriteLine("Type 'exit' to quit.");

Parser.Default.ParseArguments<YcsbCliArgs, ClusterCliArgs>(args)
   .WithParsed<ClusterCliArgs>(RunCluster)
   .WithParsed<YcsbCliArgs>(RunSingleNode)
   .WithNotParsed(errors =>
   {
       Console.WriteLine("Failed to parse command line options:");
       foreach (var error in errors)
           Console.WriteLine($" {error}");
   });

static void RunSingleNode(YcsbCliArgs settings)
{
    // ClusterCliArgs derives from YcsbCliArgs, so make sure we don't treat a cluster run as a single-node run.
    if (settings is ClusterCliArgs)
        return;

    if (!Validate(settings))
        return;

    var props = YcsbCliArgs.ParseProps(settings.Props);
    var client = DbClientFactory.Create(settings.Db, props);

    var scenario = new YcsbScenario(client);
    scenario.Run(settings);
}

static void RunCluster(ClusterCliArgs settings)
{
    if (!Validate(settings))
        return;

    ClusterRunner.Run(settings);
}

static bool Validate(YcsbCliArgs settings)
{
    var validation = new YcsbCliArgsValidator().Validate(settings);

    if (validation.IsValid)
        return true;

    Console.WriteLine("Validation failed:");
    foreach (var error in validation.Errors)
        Console.WriteLine($"  - {error.ErrorMessage}");

    return false;
}
