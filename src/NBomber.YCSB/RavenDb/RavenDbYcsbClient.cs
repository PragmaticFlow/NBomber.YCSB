using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Exceptions.Database;
using Raven.Client.Json;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;

namespace NBomber.YCSB.RavenDb;

public class DataDbRecord
{
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, string> Fields { get; set; } = [];
}

/// <summary>
/// YCSB client for RavenDB. Every record is stored as a <see cref="DataDbRecord"/> whose id is
/// "{table}/{key}" and whose collection equals the logical table name, so reads (by id) and
/// scans (by collection + id range) hit the same data the load phase produced.
/// Operations are kept to the minimum number of round trips so the benchmark reflects RavenDB's
/// achievable throughput rather than client-side overhead.
/// </summary>
public class RavenDbYcsbClient : IDbYcsbClient
{
    private readonly DocumentStore _store;
    private readonly string _databaseName;

    public RavenDbYcsbClient(Dictionary<string, string> props)
    {
        var url = YcsbCliArgs.TryGet(props, "ravendb.url", "http://localhost:8080");
        _databaseName = YcsbCliArgs.TryGet(props, "ravendb.database", "ycsb");

        _store = new DocumentStore { Urls = [url], Database = _databaseName };

        // Single-node benchmark: skip cluster topology polling.
        _store.Conventions.DisableTopologyUpdates = true;

        _store.Initialize();
    }

    public async Task<Response<object>> InitDb()
    {
        try
        {
            await _store.Maintenance.Server.SendAsync(
                new DeleteDatabasesOperation(_databaseName, hardDelete: true));
        }
        catch (DatabaseDoesNotExistException)
        {
            // nothing to delete on a fresh server
        }

        await _store.Maintenance.Server.SendAsync(
            new CreateDatabaseOperation(new DatabaseRecord(_databaseName)));

        // Warm up the connection pool before the benchmark starts.
        using var warmup = _store.OpenAsyncSession();
        await warmup.LoadAsync<DataDbRecord>("_warmup_");

        return Response.Ok<object>();
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        using var session = _store.OpenAsyncSession();

        var doc = new DataDbRecord { Id = $"{table}/{key}" };
        foreach (var kv in values)
            doc.Fields[kv.Key] = kv.Value;

        await session.StoreAsync(doc);
        session.Advanced.GetMetadataFor(doc)[Constants.Documents.Metadata.Collection] = table;
        await session.SaveChangesAsync();

        return Response.Ok<object>(sizeBytes: RavenDbHelper.GetSize(doc));
    }

    // 1 round trip: server-side patch, no Load required (YCSB update is blind-write).
    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        var id = $"{table}/{key}";

        using var session = _store.OpenAsyncSession();

        foreach (var kv in values)
        {
            // Hoist to locals: RavenDB evaluates the indexer argument as a constant, and it only
            // supports a plain string here (not a captured KeyValuePair).
            var field = kv.Key;
            var value = kv.Value;
            session.Advanced.Patch<DataDbRecord, string>(id, x => x.Fields[field], value);
        }

        await session.SaveChangesAsync();

        return Response.Ok<object>(sizeBytes: RavenDbHelper.EstimateSize(values));
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.UseOptimisticConcurrency = false;

        var doc = await session.LoadAsync<DataDbRecord>($"{table}/{key}");
        if (doc == null)
            return Response.Fail<object>(statusCode: "no data");

        return Response.Ok<object>(sizeBytes: RavenDbHelper.MeasureRead(doc, fields));
    }

    // YCSB read-modify-write (workload F): a real read followed by a write in the same session.
    // The session's change tracking turns the modified entity into a single write on SaveChanges.
    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.UseOptimisticConcurrency = false;

        var doc = await session.LoadAsync<DataDbRecord>($"{table}/{key}");
        if (doc == null)
            return Response.Fail<object>(statusCode: "no data");

        var readSize = RavenDbHelper.MeasureRead(doc, fields);

        foreach (var kv in values)
            doc.Fields[kv.Key] = kv.Value;

        await session.SaveChangesAsync();

        var writeSize = RavenDbHelper.EstimateSize(values);
        return Response.Ok<object>(sizeBytes: readSize + writeSize);
    }

    // Index-free range scan: LoadStartingWith reads documents straight from the id-ordered
    // document store (GET /docs?startsWith=), so it never touches the query/indexing engine.
    // A dynamic "where id() >= x order by id()" query would instead build an auto-index that has
    // to be rebuilt on every run (InitDb recreates the database), making scans slow and stale.
    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields)
    {
        using var session = _store.OpenAsyncSession();
        session.Advanced.UseOptimisticConcurrency = false;

        var results = await session.Advanced.LoadStartingWithAsync<DataDbRecord>(
            idPrefix: $"{table}/",
            matches: null,
            start: 0,
            pageSize: count,
            exclude: null,
            startAfter: $"{table}/{startKey}");

        var sizeBytes = results.Sum(doc => RavenDbHelper.MeasureRead(doc, fields));

        return Response.Ok<object>(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> DeleteAllData()
    {
        var op = await _store.Operations.SendAsync(
            new DeleteByQueryOperation("from @all_docs"));
        await op.WaitForCompletionAsync(TimeSpan.FromSeconds(60));
        return Response.Ok<object>();
    }

    // High-throughput load path: RavenDB's bulk insert streams documents over a single request
    // instead of one session round trip per batch.
    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        await using var bulk = _store.BulkInsert();

        foreach (var (key, values) in data)
        {
            var doc = new DataDbRecord { Id = $"{table}/{key}" };
            foreach (var kv in values)
                doc.Fields[kv.Key] = kv.Value;

            var metadata = new MetadataAsDictionary
            {
                [Constants.Documents.Metadata.Collection] = table
            };

            await bulk.StoreAsync(doc, doc.Id, metadata);
        }

        return Response.Ok<object>();
    }
}
