using MongoDB.Bson;
using MongoDB.Driver;

namespace NBomber.YCSB.MongoDb;

static class MongoDbHelper
{
    public static BsonDocument BuildDocument(string key, Dictionary<string, string> values, DateTime now)
    {
        return new BsonDocument
        {
            ["_id"] = key,
            ["fields"] = new BsonDocument(
                values.Select(kv => new BsonElement(kv.Key, kv.Value ?? string.Empty))
            ),
            ["createdAt"] = now,
            ["updatedAt"] = now
        };
    }

    public static BsonDocument BuildFieldsDocument(Dictionary<string, string> values)
    {
        return [.. values.Select(kv => new BsonElement(kv.Key, kv.Value))];
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

