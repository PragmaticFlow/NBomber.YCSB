using StackExchange.Redis;

namespace NBomber.YCSB.Redis;

public static class RedisHelper
{
    public static long GetSize(string? value)
    {
        return value is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(value);
    }

    // HashGet with a field list returns nulls for a missing key as well as for missing fields.
    public static bool AllNull(RedisValue[] values)
    {
        return values.All(v => v.IsNull);
    }

    public static long GetSize(RedisValue[] values)
    {
        return values.Aggregate(0L, (acc, v) => acc + GetSize(v));
    }

    public static long GetSize(RedisValue[][] values)
    {
        return values
            .SelectMany(arr => arr)
            .Aggregate(0L, (acc, v) => acc + GetSize(v));
    }

    public static long GetSize(HashEntry[] values)
    {
        return values.Aggregate(0L, (acc, v) => 
            acc + GetSize(v.Name) + GetSize(v.Value)
        );
    }

    public static long GetSize(HashEntry[][] values)
    {
        return values
            .SelectMany(arr => arr)
            .Aggregate(0L, (acc, v) => acc + GetSize(v.Name) + GetSize(v.Value));
    }
}
