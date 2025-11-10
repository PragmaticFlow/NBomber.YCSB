using CommandLine;

namespace NBomber.YCSB
{
    [Verb("run", HelpText = "Run a workload")]
    public class YcsbSettings
    {
        [Option( "workload", Required = true, HelpText = "Set workload type")]
        public Workload Workload { get; set; }

        [Option("recordcount", Required = false, Default = 1000, HelpText = "Number of records to insert")]
        public int RecordCount { get; set; }

        [Option("operationcount", Required = false, Default = 1000, HelpText = "Number of operations to execute")]
        public int OperationCount { get; set; }

        [Option("exportfile", Required = false, HelpText = "Export results to file")]
        public string ExportFile { get; set; }

        [Option("db", Required = false, HelpText = "Database type (redis, postgres, etc.)")]
        public string Db { get; set; }

        [Option('p', "prop", HelpText = "Set a property (key=value). Repeat for multiple properties.")]
        public IEnumerable<string> Props { get; set; } = Enumerable.Empty<string>();

        public static Dictionary<string, string> ParseProps(IEnumerable<string> props)
        {
            return props
                .Select(p => p.Split('=', 2))
                .ToDictionary(x => x[0], x => x.Length > 1 ? x[1] : "");
        }

        public static string Get(IDictionary<string, string> props, string key, string defaultValue)
            => props != null && props.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;

        public static int ParseInt(string s, int defaultValue)
            => int.TryParse(s, out var x) ? x : defaultValue;
    }
}
