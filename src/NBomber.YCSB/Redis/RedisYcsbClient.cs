using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using Spectre.Console;
using StackExchange.Redis; 

namespace NBomber.YCSB.Redis
{
    public class RedisYcsbClient : IDbYcsbClient
    {
        private readonly IDatabase _db;
        private readonly IServer _server;
        private const string HashPrefix = "hash:";
        private const string HashIndexKey = "idx:hash";

        public RedisYcsbClient(Dictionary<string, string> props)
        {
            string host = YcsbSettings.Get(props, "redis.host", "localhost");
            int port = YcsbSettings.ParseInt(YcsbSettings.Get(props, "redis.port", "6379"), 6379);

            var redis = ConnectionMultiplexer.Connect($"{host}:{port}");
            _db = redis.GetDatabase();

            var endpoints = redis.GetEndPoints();
            _server = redis.GetServer(endpoints[0]);
        }

        private static string HashKey(string key) => $"{HashPrefix}{key}";

        public async Task<Response<object>> Insert(string key, Dictionary<string, string> values)
        {
            try
            {
                var redisKey = HashKey(key);

                var entries = values
                   .Select(kv => new HashEntry(kv.Key, kv.Value ?? string.Empty))
                   .ToArray();

                var sizeBytes = GetSizeInBytes(redisKey, entries);

                await _db.HashSetAsync(redisKey, entries).ConfigureAwait(false);

                bool updateIndex = true;

                if (updateIndex)
                {
                    await _db.SortedSetAddAsync(HashIndexKey, redisKey, 0)
                                .ConfigureAwait(false);
                }

                return Response.Ok(sizeBytes: sizeBytes);
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        }

        public async Task<Response<object>> Update(string key, Dictionary<string, string> values)
        {
            try
            {
                var redisKey = HashKey(key);

                var entries = values.Select(kv => new HashEntry(kv.Key, kv.Value ?? string.Empty)).ToArray();

                var sizeBytes = GetSizeInBytes(redisKey, entries);

                await _db.HashSetAsync(redisKey, entries).ConfigureAwait(false);

                return Response.Ok(sizeBytes: sizeBytes);
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        }

        public async Task<Response<object>> Read(string key)
        {
            try
            {
                var redisKey = HashKey(key);

                var t = await _db.KeyTypeAsync(redisKey).ConfigureAwait(false);
                if (t != RedisType.Hash) return Response.Fail();

                var results = await _db.HashGetAllAsync(redisKey).ConfigureAwait(false);

                var sizeBytes = GetSizeInBytes(redisKey, results);

                return results.Length > 0 ? Response.Ok(sizeBytes: sizeBytes) : Response.Fail();
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        }

        public async Task<Response<object>> ReadLatest()
        {

            var keys = await _db.SortedSetRangeByRankAsync(HashIndexKey, 0, 0, Order.Descending)
                        .ConfigureAwait(false);
            if (keys.Length == 0) return Response.Fail();

            var key = (RedisKey)(string)keys[0];

            var all = await _db.HashGetAllAsync(key).ConfigureAwait(false);

            var sizeBytes = GetSizeInBytes((string)key, all);

            return all.Length > 0 ? Response.Ok(sizeBytes: sizeBytes) : Response.Fail();
        }

        public async Task<Response<object>> Scan(string startKey, int count)
        {
            try
            {
                var startMember = HashKey(startKey);

                var keys = await _db.SortedSetRangeByValueAsync(
                               HashIndexKey,
                               min: startMember,
                               max: "+",
                               Exclude.None,
                               Order.Ascending,
                               skip: 0,
                               take: count
                           ).ConfigureAwait(false);

                if (keys.Length == 0) return Response.Fail();

                var tasks = keys.Select(k => _db.HashGetAllAsync((RedisKey)(string)k)).ToArray();
                var hashes = await Task.WhenAll(tasks).ConfigureAwait(false);

                var sizeBytes = GetSizeInBytes(keys, hashes);

                return Response.Ok(sizeBytes: sizeBytes);
            }
            catch (Exception ex)
            {
                return Response.Fail(message: ex.Message);
            }
        }

        public async Task<Response<object>> DeleteAllData()
        {
            try
            {
                await _server.FlushDatabaseAsync().ConfigureAwait(false);

                return Response.Ok();
            }
            catch (Exception ex)
            {
                return Response.Fail<object>(message: ex.Message);
            }
        }

        public async Task<Response<object>> BulkInsert(Dictionary<string, Dictionary<string, string>> data)
        {
            try
            {
                const int chunkSize = 1000;

                foreach (var chunk in data.Chunk(chunkSize))
                {
                    var batch = _db.CreateBatch();
                    var tasks = new List<Task>();

                    foreach (var item in chunk)
                    {
                        var key = HashKey(item.Key);
                        var entries = item.Value.Select(f => new HashEntry(f.Key, f.Value ?? string.Empty)).ToArray();
                        tasks.Add(batch.HashSetAsync(key, entries));

                        tasks.Add(batch.SortedSetAddAsync(HashIndexKey, key, 0));
                    }
                     
                    batch.Execute();
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }

                return Response.Ok();
            }
            catch (Exception ex)
            {
                return Response.Fail<object>(message: ex.Message);
            }
        }

        public async Task<Response<object>> InitDb()
        {
            return Response.Ok();
        }

        private static long GetSizeInBytes(IEnumerable<(string Key, HashEntry[] Entries)> items)
        {
            long total = 0;

            foreach (var (key, entries) in items)
            {
                total += System.Text.Encoding.UTF8.GetByteCount(key);

                foreach (var e in entries)
                {
                    total += System.Text.Encoding.UTF8.GetByteCount(e.Name);
                    total += System.Text.Encoding.UTF8.GetByteCount(e.Value);
                }
            }

            return total;
        }

        private static long GetSizeInBytes(string key, HashEntry[] entries)
            => GetSizeInBytes([(key, entries)]);


        private static long GetSizeInBytes(RedisValue[] keys, HashEntry[][] hashes)
        {
            var items = keys.Zip(hashes, (k, h) => ((string)k, h));
            return GetSizeInBytes(items);
        }
    }
}