using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;
using Spectre.Console;
using StackExchange.Redis; 

namespace NBomber.YCSB.Redis;

public class RedisYcsbClient : IDbYcsbClient
{
    private readonly IDatabase _db;
    private readonly IServer _server;
    private const string INDEX_KEY = "_indices";
    private const string HASH_PREFIX = "hash:";

    public RedisYcsbClient(Dictionary<string, string> props)
    {
        try
        {
            var host = YcsbCliArgs.TryGet(props, "redis.host", defaultValue: "localhost");
            var port = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "redis.port", defaultValue: "6379"), 6379);
            var password = YcsbCliArgs.TryGet(props, "redis.password", defaultValue: "");

            var options = new ConfigurationOptions
            {
                EndPoints = { { host, port } },
                Password = password,
                AbortOnConnectFail = false,
                AllowAdmin = true,
            };

            var redis = ConnectionMultiplexer.Connect(options);
            _db = redis.GetDatabase();

            var endpoints = redis.GetEndPoints();
            _server = redis.GetServer(endpoints[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to Redis server: {ex.Message}[/]");
            throw;
        }
    }

    private static string AddKeyPrefix(string key) => $"{HASH_PREFIX}{key}";

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        var redisKey = AddKeyPrefix(key);

        var entries = values
            .Select(kv => new HashEntry(redisKey, kv.Value ?? string.Empty))
            .ToArray();

        var size = RedisHelper.GetSize(redisKey) + RedisHelper.GetSize(entries);

        await _db.HashSetAsync(key, entries);

        var index = int.Parse(key);
        await _db.SortedSetAddAsync(INDEX_KEY, key, index);
            
        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        var redisKey = AddKeyPrefix(key);

        var entries = values.
            Select(kv => new HashEntry(kv.Key, kv.Value ?? string.Empty)).
            ToArray();

        var size = RedisHelper.GetSize(redisKey) + RedisHelper.GetSize(entries);

        await _db.HashSetAsync(redisKey, entries);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string> fields)
    {
        var redisKey = AddKeyPrefix(key);

        if (fields == null || fields.Count == 0)
        {
            var entries = await _db.HashGetAllAsync(redisKey);

            var sizeBytes = RedisHelper.GetSize(redisKey) + RedisHelper.GetSize(entries);

            return entries.Length > 0 ? Response.Ok(sizeBytes: sizeBytes) : Response.Fail();
        }
        else
        {
            var redisFields = fields.Select(c => (RedisValue)c).ToArray();

            var values = await _db.HashGetAsync(redisKey, redisFields);

            var size = RedisHelper.GetSize(redisKey) + RedisHelper.GetSize(values);

            return Response.Ok(sizeBytes: size);
        }      
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string> fields)
    {
        double startIndex = int.Parse(startKey);

        var startRedisKey = AddKeyPrefix(startKey);

        // Query Redis Sorted Set to read keys in the specified range
        var keys = await _db.SortedSetRangeByScoreAsync(
            INDEX_KEY,
            start: startIndex,
            stop: double.PositiveInfinity,
            Exclude.None,
            Order.Ascending,
            skip: 0,
            take: count
        );

        if (keys.Length == 0)
        {
            return Response.Ok(sizeBytes: 0);
        }

        if (fields == null || fields.Count == 0)
        {
            var tasks = keys.Select(k => _db.HashGetAllAsync((string)k));                        

            var entries = await Task.WhenAll(tasks);
                
            var size = RedisHelper.GetSize(keys) + RedisHelper.GetSize(entries);

            return Response.Ok(sizeBytes: size);
        }
        else
        {
            var redisFields = fields.Select(c => (RedisValue)c).ToArray();

            var tasks = keys.Select(k => _db.HashGetAsync((string)k, redisFields));

            var values = await Task.WhenAll(tasks);

            var size = RedisHelper.GetSize(keys) + RedisHelper.GetSize(values);

            return Response.Ok(sizeBytes: size);
        }
    }

    public async Task<Response<object>> DeleteAllData()
    {
        await _server.FlushDatabaseAsync();

        return Response.Ok();
    }

    public async Task<Response<object>> BulkInsert(Dictionary<string, Dictionary<string, string>> data)
    {
        const int chunkSize = 1000;

        foreach (var chunk in data.Chunk(chunkSize))
        {
            var batch = _db.CreateBatch();
            var tasks = new List<Task>();

            foreach (var item in chunk)
            {
                var redisKey = AddKeyPrefix(item.Key);

                var entries = item.Value.Select(f => new HashEntry(f.Key, f.Value ?? string.Empty)).ToArray();
                    
                tasks.Add(batch.HashSetAsync(redisKey, entries));

                var index = int.Parse(item.Key);
                tasks.Add(batch.SortedSetAddAsync(INDEX_KEY, redisKey, index));
            }
                 
            batch.Execute();
            await Task.WhenAll(tasks);
        }

        return Response.Ok();
    }

    public async Task<Response<object>> InitDb()
    {
        return Response.Ok();
    }
}