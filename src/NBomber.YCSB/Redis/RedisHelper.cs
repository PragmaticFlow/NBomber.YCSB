using StackExchange.Redis;

namespace NBomber.YCSB.Redis;

static class RedisHelper
{
    internal static long GetSize(string value)
    {
        return System.Text.Encoding.UTF8.GetByteCount(value);
    }

    internal static long GetSize(RedisValue[] values)
    {
        return values.Aggregate(0L, (acc, v) => acc + GetSize(v));
    }

    internal static long GetSize(RedisValue[][] values)
    {
        return values
            .SelectMany(arr => arr)
            .Aggregate(0L, (acc, v) => acc + GetSize(v));
    }

    internal static long GetSize(HashEntry[] values)
    {
        return values.Aggregate(0L, (acc, v) => 
            acc + GetSize(v.Name) + GetSize(v.Value)
        );
    }

    internal static long GetSize(HashEntry[][] values)
    {
        return values
            .SelectMany(arr => arr)
            .Aggregate(0L, (acc, v) => acc + GetSize(v.Name) + GetSize(v.Value));
    }
}
