using NBomber.YCSB.Infra;
using Npgsql;
using System.Text;

namespace NBomber.YCSB.PosgresNoSQL;

public static class PostgresNoSQLHelper
{
    public static NpgsqlConnectionStringBuilder BuildConnectionString(Dictionary<string, string> props)
    {
        var host = YcsbCliArgs.TryGet(props, "postgres.host", defaultValue: "localhost");
        var port = YcsbCliArgs.TryGet(props, "postgres.port", defaultValue: "5432");
        var database = YcsbCliArgs.TryGet(props, "postgres.database", defaultValue: "test");
        var user = YcsbCliArgs.TryGet(props, "postgres.user", defaultValue: "postgres");
        var password = YcsbCliArgs.TryGet(props, "postgres.password", "postgres");

        return new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Database = database,
            Username = user,
            Password = password,
            MinPoolSize = 1,
            MaxPoolSize = 100,
            // Server-side prepared statements (like pgjdbc): a statement is prepared after 5 uses.
            MaxAutoPrepare = 20,
            AutoPrepareMinUsages = 5
        };
    }

    // Returns "jsonb_build_object(@f0::text, ycsb_value -> @f0::text, ...)" and adds the text parameters f0..fN,
    // so the SQL text is the same whichever fields are requested and can be prepared.
    public static string BuildProjection(IEnumerable<string> fields, NpgsqlParameterCollection parameters)
    {
        var selectors = new List<string>();

        foreach (var field in fields)
        {
            var name = $"f{selectors.Count}";

            parameters.AddWithValue(name, field);
            selectors.Add($"@{name}::text, {PostgresNoSQLYcsbClient.COLUMN_NAME} -> @{name}::text");
        }

        return $"jsonb_build_object({string.Join(", ", selectors)})";
    }

    public static long GetSize(string key, string json) =>
        Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(json);
}
