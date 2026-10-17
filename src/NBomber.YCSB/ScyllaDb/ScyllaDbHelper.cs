using Cassandra;
using System.Text;

namespace NBomber.YCSB.ScyllaDb;

internal static class ScyllaDbHelper
{
    public static long GetSize(string key, IDictionary<string, string>? fields)
    {
        var size = Encoding.UTF8.GetByteCount(key);

        if (fields != null)
            size += fields.Sum(kv => Encoding.UTF8.GetByteCount(kv.Key) + Encoding.UTF8.GetByteCount(kv.Value ?? ""));

        return size;
    }

    // Builds positional bindings: [key, field1_value, ..., field{N}_value].
    // Missing fields in `values` are bound as null so the column is written as null.
    public static object[] BuildInsertBindings(string key, string[] fieldNames, Dictionary<string, string> values)
    {
        var bindings = new object[fieldNames.Length + 1];
        bindings[0] = key;

        for (var i = 1; i < fieldNames.Length; i++)
            bindings[i] = values.TryGetValue(fieldNames[i], out var v) ? v : null!;

        return bindings;
    }

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

    public static SimpleStatement BuildProjectedRead(string key, IReadOnlyList<string> fields, string keyspace, string table, string primaryKey)
    {
        var cols = string.Join(", ", fields);

        return new SimpleStatement(
            $"SELECT {cols} FROM {keyspace}.{table} WHERE {primaryKey} = ?", key);
    }
}
