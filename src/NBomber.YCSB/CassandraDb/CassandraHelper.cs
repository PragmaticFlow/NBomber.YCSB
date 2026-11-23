using Cassandra;
using System.Text;

namespace NBomber.YCSB.CassandraDb;

public static class CassandraHelper
{
    public static long GetSize(string key, IDictionary<string, string>? fields)
    {
        var size = Encoding.UTF8.GetByteCount(key);

        if (fields != null)
            size += fields.Sum(kv => Encoding.UTF8.GetByteCount(kv.Key) + Encoding.UTF8.GetByteCount(kv.Value ?? ""));

        return size;
    }

    public static object?[] BuildInsertBindings(string key, string[] fieldNames, Dictionary<string, string> values)
    {
        var bindings = new object?[fieldNames.Length + 1];
        bindings[0] = key;

        for (var i = 0; i < fieldNames.Length; i++)
        {
            values.TryGetValue(fieldNames[i], out var value);
            bindings[i + 1] = value;
        }

        return bindings;
    }

    public static object[] BuildUpdateBindings(string key, IReadOnlyList<string> fields, Dictionary<string, string> values)
    {
        var bindings = new object[fields.Count + 1];

        for (var i = 0; i < fields.Count; i++)
            bindings[i] = values[fields[i]];

        bindings[fields.Count] = key;
        return bindings;
    }

    public static string BuildFieldsKey(IReadOnlyList<string> fields) => string.Join(",", fields);

    public static string BuildCreateKeyspaceCql(string keyspace, int replicationFactor)
        => $"CREATE KEYSPACE IF NOT EXISTS {keyspace} " +
           $"WITH replication = {{'class': 'SimpleStrategy', 'replication_factor': {replicationFactor}}}";

    public static string BuildUpdateCql(IReadOnlyList<string> fields, string keyspace, string table, string primaryKey)
    {
        var setClauses = string.Join(", ", fields.Select(f => $"{f} = ?"));
        return $"UPDATE {keyspace}.{table} SET {setClauses} WHERE {primaryKey} = ?";
    }

    public static string BuildProjectedReadCql(IReadOnlyList<string> fields, string keyspace, string table, string primaryKey)
        => $"SELECT {string.Join(", ", fields)} FROM {keyspace}.{table} WHERE {primaryKey} = ?";

    // Result columns: [primaryKey, field_1, ..., field_N]. Rows come back in token (hash) order.
    public static string BuildProjectedScanCql(IReadOnlyList<string> fields, string keyspace, string table, string primaryKey)
        => $"SELECT {primaryKey}, {string.Join(", ", fields)} FROM {keyspace}.{table} " +
           $"WHERE TOKEN({primaryKey}) >= TOKEN(?) LIMIT ?";

    public static Dictionary<string, string> ExtractFields(Row row, IReadOnlyList<string> fields, int columnOffset)
    {
        var result = new Dictionary<string, string>(fields.Count);

        for (var i = 0; i < fields.Count; i++)
        {
            var val = row.GetValue<string>(columnOffset + i);
            if (val != null) result[fields[i]] = val;
        }

        return result;
    }
}
