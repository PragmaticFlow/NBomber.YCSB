using System.Text;

namespace NBomber.YCSB.RavenDb;

static class RavenDbHelper
{
    // Size of all fields of a document (read-all-fields path).
    public static long GetSize(DataDbRecord doc)
        => doc.Fields.Sum(kv => FieldSize(kv.Key, kv.Value));

    // Size of only the requested fields (read-single-field path).
    public static long GetSize(DataDbRecord doc, HashSet<string> fields)
        => fields.Sum(f => doc.Fields.TryGetValue(f, out var v) ? FieldSize(f, v) : 0L);

    // Size of a set of field/value pairs that are about to be written.
    public static long EstimateSize(Dictionary<string, string> values)
        => values.Sum(kv => FieldSize(kv.Key, kv.Value));

    public static long MeasureRead(DataDbRecord doc, HashSet<string>? fields)
        => fields == null || fields.Count == 0
            ? GetSize(doc)
            : GetSize(doc, fields);

    private static long FieldSize(string key, string? value)
        => Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(value ?? "");

}
