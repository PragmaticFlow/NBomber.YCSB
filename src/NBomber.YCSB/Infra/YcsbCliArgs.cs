using System.CommandLine;

namespace NBomber.YCSB.Infra;

public class YcsbCliArgs
{
    public Workload Workload { get; set; }
    public ulong RecordCount { get; set; } = 1000;
    public int OperationCount { get; set; } = 1000;
    public string? ExportFile { get; set; }
    public string? Db { get; set; }
    public int ThreadCount { get; set; } = 1;
    public int FieldCount { get; set; } = 10;
    public int FieldLength { get; set; } = 100;
    public bool ReadAllFields { get; set; } = true;
    public bool WriteAllFields { get; set; } = false;
    public int ZeroPadding { get; set; } = 1;
    public string InsertOrder { get; set; } = "hashed";
    public IEnumerable<string> Props { get; set; } = [];

    private static readonly Option<Workload> WorkloadOpt = new("--workload")
    {
        Description = "Set workload type (A, B, C, D, E, F)",
        Required = true
    };
    private static readonly Option<ulong> RecordCountOpt = new("--recordcount")
    {
        Description = "Number of records to insert in the dataset at the start of the workload",
        DefaultValueFactory = _ => 1000UL
    };
    private static readonly Option<int> OperationCountOpt = new("--operationcount")
    {
        Description = "Number of operations to execute",
        DefaultValueFactory = _ => 1000
    };
    private static readonly Option<string?> ExportFileOpt = new("--exportfile")
    {
        Description = "Export results to file"
    };
    private static readonly Option<string?> DbOpt = new("--db")
    {
        Description = "Database type (redis, postgres, etc.)"
    };
    private static readonly Option<int> ThreadCountOpt = new("--threadcount")
    {
        Description = "The number of threads",
        DefaultValueFactory = _ => 1
    };
    private static readonly Option<int> FieldCountOpt = new("--fieldcount")
    {
        Description = "The number of fields in a record (default: 10)",
        DefaultValueFactory = _ => 10
    };
    private static readonly Option<int> FieldLengthOpt = new("--fieldlength")
    {
        Description = "The size of each field (default: 100)",
        DefaultValueFactory = _ => 100
    };
    private static readonly Option<bool> ReadAllFieldsOpt = new("--readallfields")
    {
        Description = "For deciding whether to read all fields (true) or just one field (false) - (default: true)",
        DefaultValueFactory = _ => true
    };
    private static readonly Option<bool> WriteAllFieldsOpt = new("--writeallfields")
    {
        Description = "For deciding whether to write all fields(true) or just one field(false) on update - (default: false)",
        DefaultValueFactory = _ => false
    };
    private static readonly Option<int> ZeroPaddingOpt = new("--zeropadding")
    {
        Description = "The name of the property for adding zero padding to record numbers in order to match string sort order. Controls the number of 0s to left pad with. (default: 1)",
        DefaultValueFactory = _ => 1
    };
    private static readonly Option<string> InsertOrderOpt = new("--insertorder")
    {
        Description = "Insert order for records: hashed or ordered (default: hashed)",
        DefaultValueFactory = _ => "hashed"
    };
    private static readonly Option<string[]> PropsOpt = new("--prop", "-p")
    {
        Description = "Set a property (key=value). Repeat for multiple properties.",
        AllowMultipleArgumentsPerToken = true
    };

    public static void AddOptionsTo(Command command)
    {
        command.Options.Add(WorkloadOpt);
        command.Options.Add(RecordCountOpt);
        command.Options.Add(OperationCountOpt);
        command.Options.Add(ExportFileOpt);
        command.Options.Add(DbOpt);
        command.Options.Add(ThreadCountOpt);
        command.Options.Add(FieldCountOpt);
        command.Options.Add(FieldLengthOpt);
        command.Options.Add(ReadAllFieldsOpt);
        command.Options.Add(WriteAllFieldsOpt);
        command.Options.Add(ZeroPaddingOpt);
        command.Options.Add(InsertOrderOpt);
        command.Options.Add(PropsOpt);
    }

    public static YcsbCliArgs FromParseResult(ParseResult p) => new()
    {
        Workload       = p.GetValue(WorkloadOpt),
        RecordCount    = p.GetValue(RecordCountOpt),
        OperationCount = p.GetValue(OperationCountOpt),
        ExportFile     = p.GetValue(ExportFileOpt),
        Db             = p.GetValue(DbOpt),
        ThreadCount    = p.GetValue(ThreadCountOpt),
        FieldCount     = p.GetValue(FieldCountOpt),
        FieldLength    = p.GetValue(FieldLengthOpt),
        ReadAllFields  = p.GetValue(ReadAllFieldsOpt),
        WriteAllFields = p.GetValue(WriteAllFieldsOpt),
        ZeroPadding    = p.GetValue(ZeroPaddingOpt),
        InsertOrder    = p.GetValue(InsertOrderOpt) ?? "hashed",
        Props          = p.GetValue(PropsOpt) ?? []
    };

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
