using Cassandra;
using System.Text;

namespace NBomber.YCSB.ScyllaDb;

public static class ScyllaDbHelper
{
    public static long GetSize(string key, IDictionary<string, string>? fields)
    {
        var size = Encoding.UTF8.GetByteCount(key);

        if (fields != null)
            size += fields.Sum(kv => Encoding.UTF8.GetByteCount(kv.Key) + Encoding.UTF8.GetByteCount(kv.Value ?? ""));

        return size;
    }

    // Builds positional bindings matching the prepared INSERT column order:
    // [key, field1_value, ..., field{N}_value] (bindings[i + 1] <-> fieldNames[i]).
    // Missing fields in `values` are bound as null so the column is written as null.
    public static object[] BuildInsertBindings(string key, string[] fieldNames, Dictionary<string, string> values)
    {
        var bindings = new object[fieldNames.Length + 1];
        bindings[0] = key;

        for (var i = 0; i < fieldNames.Length; i++)
            bindings[i + 1] = values.TryGetValue(fieldNames[i], out var v) ? v : null!;

        return bindings;
    }

    // Builds positional bindings for the UPDATE produced by BuildUpdateCql: [value_1, ..., value_N, key].
    // `fields` must be the same ordered list that was used to build the statement.
    public static object[] BuildUpdateBindings(string key, IReadOnlyList<string> fields, Dictionary<string, string> values)
    {
        var bindings = new object[fields.Count + 1];

        for (var i = 0; i < fields.Count; i++)
            bindings[i] = values[fields[i]];

        bindings[fields.Count] = key;
        return bindings;
    }

    // Cache key for a prepared statement: the ordered field list.
    public static string BuildFieldsKey(IReadOnlyList<string> fields) => string.Join(",", fields);

    public static string BuildCreateKeyspaceCql(string keyspace, int replicationFactor)
        => $"CREATE KEYSPACE IF NOT EXISTS {keyspace} " +
           $"WITH replication = {{'class': 'SimpleStrategy', 'replication_factor': {replicationFactor}}}";

    // Only the listed columns are SET; all other columns are left untouched.
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

    // Reads all field columns starting at columnOffset.
    public static Dictionary<string, string> ExtractAllFields(Row row, string[] fieldNames, int columnOffset)
    {
        var result = new Dictionary<string, string>(fieldNames.Length);

        for (var i = 0; i < fieldNames.Length; i++)
        {
            var val = row.GetValue<string>(columnOffset + i);
            if (val != null) result[fieldNames[i]] = val;
        }

        return result;
    }

    // Reads only the requested subset of columns (in SELECT order).
    public static Dictionary<string, string> ExtractFields(Row row, List<string> fields, int columnOffset)
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
