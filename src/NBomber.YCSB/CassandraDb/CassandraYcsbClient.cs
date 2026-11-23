using Cassandra;
using System.Collections.Concurrent;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.CassandraDb;

public class CassandraYcsbClient : IDbYcsbClient
{
    private readonly ISession _session;
    private readonly string _keyspace;
    private readonly int _fieldCount;
    private readonly string[] _fieldNames;

    // Max in-flight inserts in BulkInsert (the unmeasured init load), so a large recordcount does not
    // start every insert at once and overload the node; measured operations are not limited by it.
    private readonly int _maxConcurrency = 512;

    private const string TABLE_NAME = "ycsb_table";
    private const string PRIMARY_KEY = "ycsb_key";

    private PreparedStatement? _insertPs;
    private PreparedStatement? _readPs;
    private PreparedStatement? _scanPs;

    private readonly ConcurrentDictionary<string, Task<PreparedStatement>> _updatePsCache = new();
    private readonly ConcurrentDictionary<string, Task<PreparedStatement>> _projectedReadPsCache = new();
    private readonly ConcurrentDictionary<string, Task<PreparedStatement>> _projectedScanPsCache = new();

    private readonly int _replicationFactor;

    public CassandraYcsbClient(Dictionary<string, string> props)
    {
        try
        {
            var hosts = YcsbCliArgs.TryGet(props, "cassandra.hosts", defaultValue: "localhost")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var port = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "cassandra.port", defaultValue: "9043"), 9043);
            _keyspace = YcsbCliArgs.TryGet(props, "cassandra.keyspace", defaultValue: "ycsb");
            var user = YcsbCliArgs.TryGet(props, "cassandra.user", defaultValue: "");
            var password = YcsbCliArgs.TryGet(props, "cassandra.password", defaultValue: "");

            _fieldCount = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "fieldcount", defaultValue: "10"), 10);
            _fieldNames = [.. Enumerable.Range(1, _fieldCount).Select(i => $"field{i}")];

            _replicationFactor = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "cassandra.replicationfactor", defaultValue: "1"), 1);
            if (_replicationFactor < 1)
                throw new ArgumentOutOfRangeException(nameof(props), _replicationFactor, "cassandra.replicationfactor must be >= 1");

            var builder = Cluster.Builder()
                .AddContactPoints(hosts)
                .WithPort(port)
                // Java YCSB cassandra-cql binding default: QUORUM for reads and writes
                .WithQueryOptions(new QueryOptions().SetConsistencyLevel(ConsistencyLevel.Quorum));

            if (!string.IsNullOrEmpty(user))
                builder = builder.WithCredentials(user, password);

            _session = builder.Build().Connect();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to Cassandra: {ex.Message}");
            throw;
        }
    }

    public async Task<Response<object>> InitDb()
    {
        // IF NOT EXISTS does not alter the replication of an already existing keyspace.
        await _session.ExecuteAsync(new SimpleStatement(
            CassandraHelper.BuildCreateKeyspaceCql(_keyspace, _replicationFactor)
        ));

        var columnDefs = string.Join(", ", _fieldNames.Select(f => $"{f} text"));
        await _session.ExecuteAsync(new SimpleStatement(
            $"CREATE TABLE IF NOT EXISTS {_keyspace}.{TABLE_NAME} " +
            $"({PRIMARY_KEY} text PRIMARY KEY, {columnDefs})"
        ));

        _updatePsCache.Clear();
        _projectedReadPsCache.Clear();
        _projectedScanPsCache.Clear();

        await PrepareInsert();
        await PrepareRead();
        await PrepareScan();

        // Warm up single-field statements (writeallfields=false / readallfields=false) so the
        // first benchmark operations don't pay the prepare cost. Other field sets prepare lazily.
        foreach (var f in _fieldNames)
        {
            string[] single = [f];
            await GetUpdatePs(single);
            await GetProjectedReadPs(single);
            await GetProjectedScanPs(single);
        }

        return Response.Ok();
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        await _session.ExecuteAsync((_insertPs ?? await PrepareInsert()).Bind(CassandraHelper.BuildInsertBindings(key, _fieldNames, values)));
        return Response.Ok(sizeBytes: CassandraHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        await ExecuteUpdate(key, values);

        return Response.Ok(sizeBytes: CassandraHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        var result = await ReadFields(key, fields);
        if (result == null) return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: CassandraHelper.GetSize(key, result));
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        var result = await ReadFields(key, fields);
        if (result == null) return Response.Fail(statusCode: "no data");

        var size = CassandraHelper.GetSize(key, result);

        await ExecuteUpdate(key, values);

        size += CassandraHelper.GetSize(key, values);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields)
    {
        var fieldList = fields != null && fields.Count > 0 ? fields.ToList() : null;
        RowSet rowSet;

        if (fieldList == null)
        {
            rowSet = await _session.ExecuteAsync((_scanPs ?? await PrepareScan()).Bind(startKey, count));
        }
        else
        {
            var ps = await GetProjectedScanPs(fieldList);
            rowSet = await _session.ExecuteAsync(ps.Bind(startKey, count));
        }

        long sizeBytes = 0;
        foreach (var row in rowSet)
        {
            // Column 0 is always the primary key; field columns start at offset 1.
            var k = row.GetValue<string>(0);
            var result = fieldList == null
                ? CassandraHelper.ExtractFields(row, _fieldNames, columnOffset: 1)
                : CassandraHelper.ExtractFields(row, fieldList, columnOffset: 1);

            sizeBytes += CassandraHelper.GetSize(k, result);
        }

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        await Parallel.ForEachAsync(data, new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency }, async (item, _) =>
            await _session.ExecuteAsync((_insertPs ?? await PrepareInsert()).Bind(CassandraHelper.BuildInsertBindings(item.Key, _fieldNames, item.Value))));

        return Response.Ok();
    }

    private async Task<Dictionary<string, string>?> ReadFields(string key, HashSet<string>? fields)
    {
        IReadOnlyList<string> fieldList;
        PreparedStatement ps;
        if (fields == null || fields.Count == 0)
        {
            fieldList = _fieldNames;
            ps = _readPs ?? await PrepareRead();
        }
        else
        {
            fieldList = fields.ToList();
            ps = await GetProjectedReadPs(fieldList);
        }

        var row = (await _session.ExecuteAsync(ps.Bind(key))).FirstOrDefault();
        return row == null ? null : CassandraHelper.ExtractFields(row, fieldList, columnOffset: 0);
    }

    // InitDb prepares these eagerly. After DeleteAllData, InitDb must run before any operation;
    // the ?? fallbacks at the call sites only cover a client where InitDb was not called.
    private async Task<PreparedStatement> PrepareInsert()
    {
        var fieldCols = string.Join(", ", _fieldNames);
        var fieldPlaceholders = string.Join(", ", _fieldNames.Select(_ => "?"));

        return _insertPs = await _session.PrepareAsync(
            $"INSERT INTO {_keyspace}.{TABLE_NAME} ({PRIMARY_KEY}, {fieldCols}) VALUES (?, {fieldPlaceholders})"
        );
    }

    private async Task<PreparedStatement> PrepareRead()
    {
        var fieldCols = string.Join(", ", _fieldNames);

        return _readPs = await _session.PrepareAsync(
            $"SELECT {fieldCols} FROM {_keyspace}.{TABLE_NAME} WHERE {PRIMARY_KEY} = ?"
        );
    }

    private async Task<PreparedStatement> PrepareScan()
    {
        var fieldCols = string.Join(", ", _fieldNames);

        // Cassandra can't range-scan a partition key directly, so we page by
        // TOKEN(key). Note: rows come back in token (hash) order, not key order.
        return _scanPs = await _session.PrepareAsync(
            $"SELECT {PRIMARY_KEY}, {fieldCols} FROM {_keyspace}.{TABLE_NAME} " +
            $"WHERE TOKEN({PRIMARY_KEY}) >= TOKEN(?) LIMIT ?"
        );
    }

    private async Task ExecuteUpdate(string key, Dictionary<string, string> values)
    {
        var fieldList = values.Keys.ToList();
        var ps = await GetUpdatePs(fieldList);
        await _session.ExecuteAsync(ps.Bind(CassandraHelper.BuildUpdateBindings(key, fieldList, values)));
    }

    private Task<PreparedStatement> GetUpdatePs(IReadOnlyList<string> fields)
        => GetOrPrepare(_updatePsCache, fields, CassandraHelper.BuildUpdateCql);

    private Task<PreparedStatement> GetProjectedReadPs(IReadOnlyList<string> fields)
        => GetOrPrepare(_projectedReadPsCache, fields, CassandraHelper.BuildProjectedReadCql);

    private Task<PreparedStatement> GetProjectedScanPs(IReadOnlyList<string> fields)
        => GetOrPrepare(_projectedScanPsCache, fields, CassandraHelper.BuildProjectedScanCql);

    private async Task<PreparedStatement> GetOrPrepare(
        ConcurrentDictionary<string, Task<PreparedStatement>> cache, IReadOnlyList<string> fields,
        Func<IReadOnlyList<string>, string, string, string, string> buildCql)
    {
        var cacheKey = CassandraHelper.BuildFieldsKey(fields);
        // CQL text is only built on a cache miss; the static lambda + tuple arg avoid a closure per call.
        var task = cache.GetOrAdd(cacheKey,
            static (_, a) => a.self._session.PrepareAsync(a.buildCql(a.fields, a.self._keyspace, TABLE_NAME, PRIMARY_KEY)),
            (self: this, fields, buildCql));

        try
        {
            return await task;
        }
        catch
        {
            // Don't cache failures (e.g. unknown column); let the next call retry.
            cache.TryRemove(new KeyValuePair<string, Task<PreparedStatement>>(cacheKey, task));
            throw;
        }
    }

    public async Task<Response<object>> DeleteAllData()
    {
        await _session.ExecuteAsync(new SimpleStatement(
            $"DROP TABLE IF EXISTS {_keyspace}.{TABLE_NAME}"
        ));

        return Response.Ok();
    }
}
