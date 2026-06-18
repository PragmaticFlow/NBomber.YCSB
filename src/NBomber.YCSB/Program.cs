using System.CommandLine;
using NBomber.YCSB;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;
using NBomber.YCSB.MongoDb;
using NBomber.YCSB.PosgresNoSQL;
using NBomber.YCSB.Redis;

Console.WriteLine("NBomber YCSB interactive console started.");
Console.WriteLine("Type a command (for example):");
Console.WriteLine("run --workload A --recordcount 1000 --db redis -p redis.host=localhost -p redis.port=6379");
Console.WriteLine("Type 'exit' to quit.");

var runCommand = new Command("run", "Run a workload");
YcsbCliArgs.AddOptionsTo(runCommand);

runCommand.SetAction(p =>
{
    var settings = YcsbCliArgs.FromParseResult(p);

    var validator = new YcsbCliArgsValidator();
    var validation = validator.Validate(settings);

    if (!validation.IsValid)
    {
        Console.WriteLine("Validation failed:");

        foreach (var error in validation.Errors)
            Console.WriteLine($"  - {error.ErrorMessage}");

        return;
    }

    var client = GetYcsbClient(settings);
    var scenario = new YcsbScenario(client);
    scenario.Run(settings);
});

var rootCommand = new RootCommand();
rootCommand.Subcommands.Add(runCommand);
return rootCommand.Parse(args).Invoke();

static IDbYcsbClient GetYcsbClient(YcsbCliArgs settings)
{
    var propsDict = YcsbCliArgs.ParseProps(settings.Props);

    switch (settings.Db?.ToLower()) 
    {
        case "redis": 
            return new RedisYcsbClient(propsDict);
        case "mongodb":
            return new MongoDbYcsbClient(propsDict);
        case "postgres":
            return new PostgresNoSQLYcsbClient(propsDict);
        default: 
            throw new NotSupportedException($"Database '{settings.Db}' is not supported.");
    }
}
