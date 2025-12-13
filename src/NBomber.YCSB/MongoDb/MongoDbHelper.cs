using MongoDB.Bson;
using MongoDB.Driver;

namespace NBomber.YCSB.MongoDb;

static class MongoDbHelper
{
    internal static BsonDocument BuildDocument(string key, Dictionary<string, string> values, DateTime now)
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

    internal static FilterDefinition<BsonDocument> BuildFilter(string key)
    {
        return Builders<BsonDocument>.Filter.Eq("_id", key);
    }

    internal static long GetSize(BsonDocument doc)
    {
        return doc.ToBson().Length;
    }
}

