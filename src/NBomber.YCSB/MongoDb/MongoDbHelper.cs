using MongoDB.Bson;
using MongoDB.Driver;

namespace NBomber.YCSB.MongoDb;

public static class MongoDbHelper
{
    public static BsonDocument BuildDocument(string key, Dictionary<string, string> values)
    {
        var doc = new BsonDocument("_id", key);

        foreach (var kv in values)
            doc[kv.Key] = kv.Value ?? string.Empty;

        return doc;
    }

    public static BsonDocument BuildFieldsDocument(Dictionary<string, string> values)
    {
        return [.. values.Select(kv => new BsonElement(kv.Key, kv.Value ?? string.Empty))];
    }

    public static BsonDocument BuildScanFilter(string startKey)
    {
        return new BsonDocument("_id", new BsonDocument("$gte", startKey));
    }

    public static BsonDocument? BuildProjection(HashSet<string>? fields)
    {
        return fields == null || fields.Count == 0
            ? null
            : new BsonDocument(fields.Select(f => new BsonElement(f, 1)));
    }

    public static BsonDocument BuildUpdate(Dictionary<string, string> values)
    {
        return new BsonDocument("$set", BuildFieldsDocument(values));
    }

    public static FilterDefinition<BsonDocument> BuildFilter(string key)
    {
        return Builders<BsonDocument>.Filter.Eq("_id", key);
    }

    public static long GetSize(BsonDocument doc)
    {
        return doc.ToBson().Length;
    }
}

