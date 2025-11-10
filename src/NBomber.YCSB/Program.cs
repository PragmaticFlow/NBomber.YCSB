using NBomber.YCSB;
using NBomber.YCSB.Redis;
using CommandLine;

Console.WriteLine("NBomber YCSB interactive console started.");
Console.WriteLine("Type a command (for example):");
Console.WriteLine("run --workload A --recordcount 1000 --db redis -p redis.host=localhost redis.port=6379");
Console.WriteLine("Type 'exit' to quit.");

Parser.Default.ParseArguments<YcsbSettings>(args)
   .WithParsed(async settings =>
   {
       var propsDict = YcsbSettings.ParseProps(settings.Props);

       var client = new RedisYcsbClient(propsDict);

       var scenario = new BaseScenario(client);
       scenario.Run(settings);
   })
   .WithNotParsed(errors =>
   {
       Console.WriteLine("Failed to parse command line options:");
       foreach (var error in errors)
           Console.WriteLine($"  {error}");
   });