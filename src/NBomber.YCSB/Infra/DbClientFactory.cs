using NBomber.YCSB.DAL;
using NBomber.YCSB.MongoDb;
using NBomber.YCSB.PosgresNoSQL;
using NBomber.YCSB.Redis;

namespace NBomber.YCSB.Infra;

/// <summary>
/// Creates <see cref="IDbYcsbClient"/> instances for the supported databases.
/// Shared by the single-node runner and the cluster runner.
/// </summary>
public static class DbClientFactory
{
    public static readonly string[] SupportedDatabases = ["redis", "mongodb", "postgres"];

    public static IDbYcsbClient Create(string? db, Dictionary<string, string> props)
    {
        return db?.ToLower() switch
        {
            "redis" => new RedisYcsbClient(props),
            "mongodb" => new MongoDbYcsbClient(props),
            "postgres" => new PostgresNoSQLYcsbClient(props),
            _ => throw new NotSupportedException($"Database '{db}' is not supported.")
        };
    }
}
