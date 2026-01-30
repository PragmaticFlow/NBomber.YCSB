using StackExchange.Redis;

namespace NBomber.YCSB.PosgresNoSQL
{
    public class PostgresNoSQLHelper
    {
        public static long GetSize(string value)
        {
            return System.Text.Encoding.UTF8.GetByteCount(value);
        }
    }
}
