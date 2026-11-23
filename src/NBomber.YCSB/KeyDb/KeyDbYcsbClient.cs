using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;
using StackExchange.Redis; 

namespace NBomber.YCSB.KeyDb;

public class KeyDbYcsbClient : IDbYcsbClient, IDisposable
{
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly IServer _server;
    private const string INDEX_KEY = "_indices";
    private const string HASH_PREFIX = "hash:";

    public KeyDbYcsbClient(Dictionary<string, string> props)
    {
        try
        {
            var host = YcsbCliArgs.TryGet(props, "keydb.host", defaultValue: "localhost");
            var port = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "keydb.port", defaultValue: "6380"), 6380);
            var password = YcsbCliArgs.TryGet(props, "keydb.password", defaultValue: "");

            var options = new ConfigurationOptions
            {
                EndPoints = { { host, port } },
                Password = password,
                AbortOnConnectFail = false,
                AllowAdmin = true,
            };

            _redis = ConnectionMultiplexer.Connect(options);
            _db = _redis.GetDatabase();

            var endpoints = _redis.GetEndPoints();
            _server = _redis.GetServer(endpoints[0]);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to KeyDB server: {ex.Message}");
            throw;
        }
    }

    private static string AddKeyPrefix(string key) => $"{HASH_PREFIX}{key}";

    public void Dispose() => _redis.Dispose();

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        var redisKey = AddKeyPrefix(key);

        var entries = values
            .Select(kv => new HashEntry(kv.Key, kv.Value ?? string.Empty))
            .ToArray();

        var size = KeyDbHelper.GetSize(redisKey) + KeyDbHelper.GetSize(entries);

        await _db.HashSetAsync(redisKey, entries);

        await _db.SortedSetAddAsync(INDEX_KEY, key, 0);
            
        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        var redisKey = AddKeyPrefix(key);

        var entries = values.
            Select(kv => new HashEntry(kv.Key, kv.Value ?? string.Empty)).
            ToArray();

        var size = KeyDbHelper.GetSize(redisKey) + KeyDbHelper.GetSize(entries);

        await _db.HashSetAsync(redisKey, entries);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string> fields)
    {
        var redisKey = AddKeyPrefix(key);

        if (fields == null || fields.Count == 0)
        {
            var entries = await _db.HashGetAllAsync(redisKey);

            var sizeBytes = KeyDbHelper.GetSize(redisKey) + KeyDbHelper.GetSize(entries);

            return entries.Length > 0 
                ? Response.Ok(sizeBytes: sizeBytes) 
                : Response.Fail(statusCode: "no data");
        }
        else
        {
            var fieldNames = fields.Select(c => (RedisValue)c).ToArray();

            var values = await _db.HashGetAsync(redisKey, fieldNames);

            var size = KeyDbHelper.GetSize(redisKey) + KeyDbHelper.GetSize(values);

            return KeyDbHelper.AllNull(values)
                ? Response.Fail(statusCode: "no data")
                : Response.Ok(sizeBytes: size);
        }
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        var redisKey = AddKeyPrefix(key);

        // Read phase
        long size;

        if (fields == null || fields.Count == 0)
        {
            var readEntries = await _db.HashGetAllAsync(redisKey);
            size = KeyDbHelper.GetSize(redisKey) + KeyDbHelper.GetSize(readEntries);

            if (readEntries.Length == 0)
                return Response.Fail(statusCode: "no data");
        }
        else
        {
            var fieldNames = fields.Select(c => (RedisValue)c).ToArray();
            var readValues = await _db.HashGetAsync(redisKey, fieldNames);
            size = KeyDbHelper.GetSize(redisKey) + KeyDbHelper.GetSize(readValues);

            if (KeyDbHelper.AllNull(readValues))
                return Response.Fail(statusCode: "no data");
        }

        // Update phase
        var updateEntries = values
            .Select(kv => new HashEntry(kv.Key, kv.Value ?? string.Empty))
            .ToArray();

        size += KeyDbHelper.GetSize(redisKey) + KeyDbHelper.GetSize(updateEntries);

        await _db.HashSetAsync(redisKey, updateEntries);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string> fields)
    {
        // Query the sorted set to read keys in the specified range
        var keys = await _db.SortedSetRangeByValueAsync(
            INDEX_KEY,
            min: startKey,
            max: default,
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
            var tasks = keys.Select(k => {
                var kPrefix = AddKeyPrefix((string)k);
                return _db.HashGetAllAsync(kPrefix);
            });

            var entries = await Task.WhenAll(tasks);
                
            var size = KeyDbHelper.GetSize(keys) + KeyDbHelper.GetSize(entries);

            return Response.Ok(sizeBytes: size);
        }
        else
        {
            var redisFields = fields.Select(c => (RedisValue)c).ToArray();

            var tasks = keys.Select(k => {
                var kPrefix = AddKeyPrefix((string)k);
                return _db.HashGetAsync(kPrefix, redisFields);
            });

            var values = await Task.WhenAll(tasks);

            var size = KeyDbHelper.GetSize(keys) + KeyDbHelper.GetSize(values);

            return Response.Ok(sizeBytes: size);
        }
    }

    public async Task<Response<object>> DeleteAllData()
    {
        await _server.FlushDatabaseAsync();

        return Response.Ok();
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
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
                tasks.Add(batch.SortedSetAddAsync(INDEX_KEY, item.Key, 0));
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