# Design

## Context

`MongoDbYcsbClient` stores `{ _id, fields: { fieldN }, createdAt, updatedAt }`, scans with `_id == startKey`, projects top-level `fieldN` (wrong path) and throws on a read miss. Java YCSB stores flat documents, scans `$gte` + sort + limit, and returns NOT_FOUND.

## Goals / Non-Goals

- Goals: Java-equivalent layout, scan, projection and miss handling; `w:1`; minimal diff inside `MongoDb/*`.
- Non-goals: `DeleteAllData` scope; BSON Binary values; collection name; Core or `IDbYcsbClient` changes.

## Decisions

### MongoDbHelper (public static, like RedisHelper/ScyllaDbHelper)
```csharp
public static BsonDocument BuildDocument(string key, Dictionary<string, string> values)
{
    var doc = new BsonDocument("_id", key);
    foreach (var kv in values) doc[kv.Key] = kv.Value ?? string.Empty;
    return doc;
}
public static BsonDocument BuildFieldsDocument(Dictionary<string, string> values)  // ?? string.Empty
public static FilterDefinition<BsonDocument> BuildFilter(string key)              // unchanged (Eq _id)
public static BsonDocument BuildScanFilter(string startKey) => new("_id", new BsonDocument("$gte", startKey));
public static BsonDocument? BuildProjection(HashSet<string>? fields)               // null => all fields
    => fields == null || fields.Count == 0 ? null : new BsonDocument(fields.Select(f => new BsonElement(f, 1)));
public static BsonDocument BuildUpdate(Dictionary<string, string> values) => new("$set", BuildFieldsDocument(values));
public static long GetSize(BsonDocument doc)                                       // unchanged
```
`BsonDocument` converts implicitly to filter/projection/update definitions.

### MongoDbYcsbClient
- Constructor: `WriteConcern = WriteConcern.W1` in `MongoClientSettings`.
- Insert/BulkInsert: `BuildDocument(key, values)`.
- Read: one path — `Find(BuildFilter(key))`, `Project` only when `BuildProjection` is not null, `FirstOrDefaultAsync`; null → `Fail("no data")`, else `Ok(sizeBytes: GetSize(doc))`.
- Scan: `Find(BuildScanFilter(startKey))`, optional projection, `Sort(Ascending("_id")).Limit(count).ToListAsync()`; empty → `Fail("no data")`, else `Ok(sizeBytes: sum)`.
- Update: `UpdateOneAsync(BuildFilter(key), BuildUpdate(values))`; `MatchedCount == 0` → `Fail("no data")` (unchanged).
- ReadModifyWrite: read as in Read (miss → `Fail("no data")`), then `UpdateOneAsync(BuildFilter, BuildUpdate)`.
- `DeleteAllData`, `InitDb`: unchanged.

### Hot path
Removes `DateTime.UtcNow` and the extra `$set`; no new allocations beyond the projection document (built per call as before).

### Destructive operations
None new. `DeleteAllData` (drops non-system databases) is unchanged.

## Risks / Trade-offs

- [MongoDB results change, workload E most] → Intended; documented in CLAUDE.md; earlier MongoDB results not directly comparable.
- [Existing documents with the old layout] → None survive: every run drops the database before loading.
- [Empty scan now returns "no data"] → Matches Java (ERROR on empty scan) and the client contract; start keys come from existing records, so it is rare.

## Migration Plan

None. Rollback is reverting the change.
