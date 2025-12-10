using NBomber.YCSB;
using NBomber.YCSB.Redis;
using CommandLine;
using NBomber.YCSB.Infra;

Console.WriteLine("NBomber YCSB interactive console started.");
Console.WriteLine("Type a command (for example):");
Console.WriteLine("run --workload A --recordcount 1000 --db redis -p redis.host=localhost -p redis.port=6379");
Console.WriteLine("Type 'exit' to quit.");

Parser.Default.ParseArguments<YcsbCliArgs>(args)
   .WithParsed(settings =>
   {
       var argsValidator = new YcsbCliArgsValidator();

       var validation = argsValidator.Validate(settings);

       if (!validation.IsValid)
       {
           Console.WriteLine("Validation failed:");
           foreach (var error in validation.Errors)
               Console.WriteLine($"  - {error.ErrorMessage}");
           return;
       }

       var propsDict = YcsbCliArgs.ParseProps(settings.Props);

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