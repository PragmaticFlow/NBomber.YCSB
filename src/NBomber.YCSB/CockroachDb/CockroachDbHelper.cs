using NBomber.YCSB.Infra;
using Npgsql;
using System.Text;

namespace NBomber.YCSB.CockroachDb;

public static class CockroachDbHelper
{
    public static NpgsqlConnectionStringBuilder BuildConnectionString(Dictionary<string, string> props)
    {
        var host = YcsbCliArgs.TryGet(props, "cockroachdb.host", defaultValue: "localhost");
        var port = YcsbCliArgs.TryGet(props, "cockroachdb.port", defaultValue: "26257");
        var database = YcsbCliArgs.TryGet(props, "cockroachdb.database", defaultValue: "defaultdb");
        var user = YcsbCliArgs.TryGet(props, "cockroachdb.user", defaultValue: "root");
        var password = YcsbCliArgs.TryGet(props, "cockroachdb.password", defaultValue: "");

        return new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Database = database,
            Username = user,
            Password = password,
            MinPoolSize = 1,
            MaxPoolSize = 100,
            MaxAutoPrepare = 20,
            AutoPrepareMinUsages = 5
        };
    }

    public static string BuildProjection(IEnumerable<string> fields, NpgsqlParameterCollection parameters)
    {
        var selectors = new List<string>();

        foreach (var field in fields)
        {
            var name = $"f{selectors.Count}";

            parameters.AddWithValue(name, field);
            selectors.Add($"@{name}::text, {CockroachDbYcsbClient.COLUMN_NAME} -> @{name}::text");
        }

        return $"jsonb_build_object({string.Join(", ", selectors)})";
    }

    public static long GetSize(string key, string json) =>
        Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(json);
}
