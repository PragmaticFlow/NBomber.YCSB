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
            Server = new MongoServerAddress(host, int.Parse(port)),
            WriteConcern = WriteConcern.W1
        };

        _client = new MongoClient(mongoClientSett);
        _db = _client.GetDatabase(databaseName);
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var doc = MongoDbHelper.BuildDocument(key, values);

        await col.InsertOneAsync(doc);

        var size = MongoDbHelper.GetSize(doc);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var filter = MongoDbHelper.BuildFilter(key);

        var result = await col.UpdateOneAsync(filter, MongoDbHelper.BuildUpdate(values));

        if (result.MatchedCount == 0)
            return Response.Fail(statusCode: "no data");

        var size = MongoDbHelper.GetSize(MongoDbHelper.BuildFieldsDocument(values));

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var doc = await FindOne(col, key, fields);

        if (doc == null)
            return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: MongoDbHelper.GetSize(doc));
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string> fields)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var find = col.Find<BsonDocument>(MongoDbHelper.BuildScanFilter(startKey));

        var projection = MongoDbHelper.BuildProjection(fields);
        if (projection != null)
            find = find.Project<BsonDocument>(projection);

        var result = await find
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .Limit(count)
            .ToListAsync();

        if (result.Count == 0)
            return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: result.Sum(MongoDbHelper.GetSize));
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        var col = _db.GetCollection<BsonDocument>(table);
        var filter = MongoDbHelper.BuildFilter(key);

        var existing = await FindOne(col, key, fields);

        if (existing == null)
            return Response.Fail(statusCode: "no data");

        var size = MongoDbHelper.GetSize(existing);

        await col.UpdateOneAsync(filter, MongoDbHelper.BuildUpdate(values));

        size += MongoDbHelper.GetSize(MongoDbHelper.BuildFieldsDocument(values));

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> DeleteAllData()
    {
        await _client.DropDatabaseAsync(databaseName);
        return Response.Ok();
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        var col = _db.GetCollection<BsonDocument>(table);

        var docs = data.Select(kvp => MongoDbHelper.BuildDocument(kvp.Key, kvp.Value))
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

    private static async Task<BsonDocument?> FindOne(IMongoCollection<BsonDocument> col, string key, HashSet<string>? fields)
    {
        var find = col.Find(MongoDbHelper.BuildFilter(key));

        var projection = MongoDbHelper.BuildProjection(fields);
        if (projection != null)
            find = find.Project<BsonDocument>(projection);

        return await find.FirstOrDefaultAsync();
    }
}
