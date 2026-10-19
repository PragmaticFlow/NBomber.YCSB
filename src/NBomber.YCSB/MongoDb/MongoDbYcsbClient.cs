using MongoDB.Bson;
using MongoDB.Driver;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;
using Spectre.Console;

namespace NBomber.YCSB.MongoDb;

public class MongoDbYcsbClient : IDbYcsbClient
{
    private readonly IMongoDatabase _db;
    private readonly MongoClient _client;
    private readonly string databaseName = "ycsb";

    public MongoDbYcsbClient(Dictionary<string, string> props)
    {
        var host = YcsbCliArgs.TryGet(props, "mongodb.host", defaultValue: "localhost");
        var port = YcsbCliArgs.TryGet(props, "mongodb.port", defaultValue: "27017");

        var mongoClientSett = new MongoClientSettings()
        {
            Server = new MongoServerAddress(host, int.Parse(port))
        };

        _client = new MongoClient(mongoClientSett);
        _db = _client.GetDatabase(databaseName);
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var doc = MongoDbHelper.BuildDocument(key, values, DateTime.UtcNow);

        await col.InsertOneAsync(doc);

        var size = MongoDbHelper.GetSize(doc);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var filter = MongoDbHelper.BuildFilter(key);

        var updates = values
            .Select(kv =>
                Builders<BsonDocument>.Update.Set($"fields.{kv.Key}", kv.Value ?? string.Empty)
            ).ToList();

        updates.Add(Builders<BsonDocument>.Update.Set("updatedAt", DateTime.UtcNow));

        var update = Builders<BsonDocument>.Update.Combine(updates);

        var result = await col.UpdateOneAsync(filter, update);

        if (result.MatchedCount == 0)
            return Response.Fail(statusCode: "no data");

        var size = MongoDbHelper.GetSize(MongoDbHelper.BuildFieldsDocument(values));

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var filter = MongoDbHelper.BuildFilter(key);

        if (fields == null || fields.Count == 0)
        {
            var result = await col.Find(filter).FirstOrDefaultAsync();

            var size = MongoDbHelper.GetSize(result);

            return Response.Ok(sizeBytes: size);
        }
        else
        {
            var projection = fields.Aggregate(
                                Builders<BsonDocument>.Projection.Include("_id"),
                                (p, f) => p.Include(f)
                            );

            var result = await col
                .Find(filter)
                .Project<BsonDocument>(projection)
                .FirstOrDefaultAsync();

            var size = MongoDbHelper.GetSize(result);

            return Response.Ok(sizeBytes: size);
        }
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string> fields)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var filter = MongoDbHelper.BuildFilter(startKey);

        if (fields == null || fields.Count == 0)
        {
            var result = await col
                  .Find(filter)
                  .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                  .Limit(count)
                  .ToListAsync();

            var sizeBytes = result.Sum(MongoDbHelper.GetSize);

            return Response.Ok(sizeBytes: sizeBytes);
        }
        else
        {
            var projection = fields.Aggregate(
                Builders<BsonDocument>.Projection.Include("_id"), (p, f) => p.Include(f)
            );

            var result = await col
                   .Find(filter)
                   .Project<BsonDocument>(projection)
                   .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
                   .Limit(count)
                   .ToListAsync();

            var sizeBytes = result.Sum(MongoDbHelper.GetSize);

            return Response.Ok(sizeBytes: sizeBytes);
        }
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        var col = _db.GetCollection<BsonDocument>(table);
        var filter = MongoDbHelper.BuildFilter(key);

        // Read phase
        BsonDocument existing;

        if (fields == null || fields.Count == 0)
        {
            existing = await col.Find(filter).FirstOrDefaultAsync();
        }
        else
        {
            var projection = fields.Aggregate(
                Builders<BsonDocument>.Projection.Include("_id"),
                (p, f) => p.Include(f)
            );

            existing = await col.Find(filter).Project<BsonDocument>(projection).FirstOrDefaultAsync();
        }

        if (existing == null)
            return Response.Fail(statusCode: "no data");

        var size = MongoDbHelper.GetSize(existing);

        // Update phase
        var updates = values
            .Select(kv =>
                Builders<BsonDocument>.Update.Set($"fields.{kv.Key}", kv.Value ?? string.Empty)
            ).ToList();

        updates.Add(Builders<BsonDocument>.Update.Set("updatedAt", DateTime.UtcNow));

        var update = Builders<BsonDocument>.Update.Combine(updates);
        await col.UpdateOneAsync(filter, update);

        size += MongoDbHelper.GetSize(MongoDbHelper.BuildFieldsDocument(values));

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> DeleteAllData()
    {
        var databases = _client.ListDatabaseNames().ToList();
        foreach (var dbName in databases)
        {
            if (dbName == "admin" || dbName == "local" || dbName == "config")
                continue;

            await _client.DropDatabaseAsync(dbName);
        }
        return Response.Ok();
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        var col = _db.GetCollection<BsonDocument>(table);
        var now = DateTime.UtcNow;

        var docs = data.Select(kvp => MongoDbHelper.BuildDocument(kvp.Key, kvp.Value, now))
                       .ToList();

        // MongoDB continues inserting the rest of the documents even if one fails
        var opts = new InsertManyOptions { IsOrdered = false };

        await col.InsertManyAsync(docs, opts).ConfigureAwait(false);

        return Response.Ok();
    }

    public async Task<Response<object>> InitDb()
    {
        return Response.Ok();
    }
}
