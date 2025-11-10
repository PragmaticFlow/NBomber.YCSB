//using MongoDB.Bson;
//using MongoDB.Driver;
//using NBomber.YCSB.DAL;

//namespace NBomber.YCSB.MongoDb
//{
//    public class MongoDbYcsbClient : IDbYcsbClient
//    {
//        private readonly IMongoCollection<BsonDocument> _col;

//        public MongoDbYcsbClient(string connectionString, string databaseName, string collectionName)
//        {
//            var client = new MongoClient(connectionString);
//            var db = client.GetDatabase(databaseName);
//            _col = db.GetCollection<BsonDocument>(collectionName);
//        }

//        private async Task EnsureIndexesAsync()
//        {
//            var idxModel = new CreateIndexModel<BsonDocument>(
//                Builders<BsonDocument>.IndexKeys.Descending("createdAt"),
//                new CreateIndexOptions { Background = true, Name = "ix_createdAt_desc" }
//            );
//            await _col.Indexes.CreateOneAsync(idxModel).ConfigureAwait(false);
//        }

//        public async Task<Status> BulkInsert(Dictionary<string, Dictionary<string, string>> data)
//        {
//            try
//            {
//                var now = DateTime.UtcNow;

//                var docs = data.Select(kvp => new BsonDocument
//                {
//                    ["_id"] = kvp.Key,
//                    ["fields"] = new BsonDocument(kvp.Value.Select(f =>
//                        new BsonElement(f.Key, f.Value ?? string.Empty))),
//                    ["createdAt"] = now,
//                    ["updatedAt"] = now
//                }).ToList();

//                var opts = new InsertManyOptions { IsOrdered = false };
//                await _col.InsertManyAsync(docs, opts).ConfigureAwait(false);

//                return Status.Ok;
//            }
//            catch 
//            { 
//                return Status.Error;
//            }
//        }

//        public async Task<Status> DeleteAllData()
//        {
//            try
//            {
//                await _col.Database.DropCollectionAsync(_col.CollectionNamespace.CollectionName)
//                                   .ConfigureAwait(false);
//                return Status.Ok;
//            }
//            catch
//            {
//                return Status.Error;
//            }
//        }

//        public async Task<Status> InitDb()
//        {
//            try
//            {
//                await EnsureIndexesAsync().ConfigureAwait(false);
//                return Status.Ok;
//            }
//            catch
//            {
//                return Status.Error;
//            }
//        }

//        public async Task<Status> Insert(string key, Dictionary<string, string> values)
//        {
//            try
//            {
//                var now = DateTime.UtcNow;

//                var doc = new BsonDocument
//                {
//                    ["_id"] = $"new_{key}",
//                    ["fields"] = new BsonDocument(values.Select(kv =>
//                        new BsonElement(kv.Key, kv.Value ?? string.Empty))),
//                    ["createdAt"] = now,
//                    ["updatedAt"] = now
//                };

//                await _col.InsertOneAsync(doc).ConfigureAwait(false);
//                return Status.Ok;
//            }
//            catch
//            { 
//                return Status.Error;
//            }
//        }

//        public async Task<Status> Read(string key)
//        {
//            try
//            {
//                var doc = await _col
//                    .Find(Builders<BsonDocument>.Filter.Eq("_id", key))
//                    .FirstOrDefaultAsync()
//                    .ConfigureAwait(false);

//                if (doc is null) return Status.Error;

//                var fields = doc.GetValue("fields", null)?.AsBsonDocument;
//                return (fields != null && fields.ElementCount > 0) ? Status.Ok : Status.Error;
//            }
//            catch
//            {
//                return Status.Error;
//            }
//        }

//        public async Task<Status> ReadLatest()
//        {
//            try
//            {
//                var doc = await _col
//                    .Find(FilterDefinition<BsonDocument>.Empty)
//                    .Sort(Builders<BsonDocument>.Sort.Descending("createdAt"))
//                    .Limit(1)
//                    .FirstOrDefaultAsync()
//                    .ConfigureAwait(false);

//                if (doc is null) return Status.Error;

//                var fields = doc.GetValue("fields", null)?.AsBsonDocument;
//                return (fields != null && fields.ElementCount > 0) ? Status.Ok : Status.Error;
//            }
//            catch
//            {
//                return Status.Error;
//            }
//        }

//        public async Task<Status> Scan(string startKey, int count)
//        {
//            try
//            {
//                var filter = Builders<BsonDocument>.Filter.Gte("_id", startKey);

//                var docs = await _col
//                    .Find(filter)
//                    .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
//                    .Limit(count)
//                    .ToListAsync()
//                    .ConfigureAwait(false);

//                if (docs.Count == 0) return Status.Error;

//                var hasAnyFields = docs.Any(d => d.GetValue("fields", null)?.AsBsonDocument?.ElementCount > 0);
//                return hasAnyFields ? Status.Ok : Status.Error;
//            }
//            catch
//            {
//                return Status.Error;
//            }
//        }

//        public async Task<Status> Update(string key, Dictionary<string, string> values)
//        {
//            try
//            {
//                var updateFields = new BsonDocument(values.Select(kv =>
//                    new BsonElement($"fields.{kv.Key}", kv.Value ?? string.Empty)));

//                var update = new UpdateDefinitionBuilder<BsonDocument>()
//                    .SetOnInsert("createdAt", DateTime.UtcNow)
//                    .Set("updatedAt", DateTime.UtcNow)
//                    .Inc("_touch", 1);

//                foreach (var elem in updateFields.Elements)
//                    update = update.Set(elem.Name, elem.Value);

//                var res = await _col.UpdateOneAsync(
//                    Builders<BsonDocument>.Filter.Eq("_id", key),
//                    update,
//                    new UpdateOptions { IsUpsert = true }
//                ).ConfigureAwait(false);

//                return Status.Ok;
//            }
//            catch
//            {
//                return Status.Error;
//            }
//        }
//    }
//}
