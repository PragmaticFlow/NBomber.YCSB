using CommandLine;

namespace NBomber.YCSB.Infra;

[Verb("run", HelpText = "Run a workload")]
public class YcsbCliArgs
{
    [Option( "workload", Required = true, HelpText = "Set workload type (A, B, C, D, E)")]
    public Workload Workload { get; set; }

    [Option("recordcount", Required = false, Default = 1000, HelpText = "Number of records to insert in the dataset at the start of the workload")]
    public ulong RecordCount { get; set; }

    [Option("operationcount", Required = false, Default = 1000, HelpText = "Number of operations to execute")]
    public int OperationCount { get; set; }

    [Option("exportfile", Required = false, HelpText = "Export results to file")]
    public string ExportFile { get; set; }

    [Option("db", Required = false, HelpText = "Database type (redis, postgres, etc.)")]
    public string Db { get; set; }

    [Option("threadcount", Required = false, Default = 1, HelpText = "The number of threads")]
    public int ThreadCount { get; set; }

    [Option("fieldcount", Required = false, Default = 10, HelpText = "The number of fields in a record (default: 10)")]
    public int FieldCount { get; set; }

    [Option("fieldlength", Required = false, Default = 100, HelpText = "The size of each field (default: 100)")]
    public int FieldLength { get; set; }

    [Option("readallfields", Required = false, Default = true, HelpText = "For deciding whether to read all fields (true) or just one field (false) - (default: true)")]
    public bool ReadAllFields { get; set; }

    [Option("writeallfields", Required = false, Default = false, HelpText = "For deciding whether to write all fields (true) or just one field (false) on update - (default: false)")]
    public bool WriteAllFields { get; set; }

    [Option("zeropadding", Required = false, Default = 1, HelpText = "The name of the property for adding zero padding to record numbers in order to match string sort order. Controls the number of 0s to left pad with. (default: 1)")]
    public int ZeroPadding { get; set; }

    [Option("insertorder", Required = false, Default = "hashed", HelpText = "The name of the property for adding zero padding to record numbers in order to match string sort order. Controls the number of 0s to left pad with. (default: 1)")]
    public required string InsertOrder { get; set; }

    [Option('p', "prop", Required = false, Separator = ';', HelpText = "Set a property (key=value). Repeat for multiple properties.")]
    public IEnumerable<string> Props { get; set; } = Enumerable.Empty<string>();

    public static Dictionary<string, string> ParseProps(IEnumerable<string> props)
    {
        return props
            .Select(p => p.Split('=', 2))
            .ToDictionary(x => x[0], x => x.Length > 1 ? x[1] : "");
    }

    public static string TryGet(IDictionary<string, string> props, string key, string defaultValue)
        => props != null && props.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : defaultValue;

    public static int TryParseInt(string s, int defaultValue)
        => int.TryParse(s, out var x) ? x : defaultValue;
}
