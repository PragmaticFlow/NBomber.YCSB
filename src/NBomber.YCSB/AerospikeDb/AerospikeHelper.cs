using Aerospike.Client;
using System.Text;

namespace NBomber.YCSB.AerospikeDb;

public static class AerospikeHelper
{
    public static long GetSize(string key, IDictionary<string, string>? fields)
    {
        var size = (long)Encoding.UTF8.GetByteCount(key);

        if (fields != null)
            foreach (var kv in fields)
                size += Encoding.UTF8.GetByteCount(kv.Key) + Encoding.UTF8.GetByteCount(kv.Value ?? "");

        return size;
    }

    public static long GetSize(string key, Record record)
    {
        var size = (long)Encoding.UTF8.GetByteCount(key);

        foreach (var bin in record.bins)
            size += Encoding.UTF8.GetByteCount(bin.Key) + Encoding.UTF8.GetByteCount(bin.Value as string ?? "");

        return size;
    }

    public static Bin[] BuildBins(Dictionary<string, string> values)
    {
        var bins = new Bin[values.Count];
        var i = 0;

        foreach (var kv in values)
            bins[i++] = new Bin(kv.Key, kv.Value ?? "");

        return bins;
    }

    public static int GetMaxConcurrency(Dictionary<string, string> props)
    {
        if (!props.TryGetValue("aerospike.maxconcurrency", out var raw))
            return 512;

        if (int.TryParse(raw, out var value) && value >= 1)
            return value;

        throw new ArgumentException($"aerospike.maxconcurrency must be a positive integer, got '{raw}'.", nameof(props));
    }

    // "namespaces" info reply is a ';' separated list, e.g. "test;bar".
    public static bool HasNamespace(string namespacesInfo, string ns)
        => namespacesInfo.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(ns);
}
