using Cassandra;
using System.Collections.Concurrent;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.ScyllaDb;

public class ScyllaYcsbClient : IDbYcsbClient
{
    private readonly ISession _session;
    private readonly string _keyspace;
    private readonly int _fieldCount;
    private readonly string[] _fieldNames;

    private readonly int _maxConcurrency = 512;

    private const string TABLE_NAME = "ycsb_table";
    private const string PRIMARY_KEY = "ycsb_key";

    private PreparedStatement? _insertPs;
    private PreparedStatement? _readPs;
    private PreparedStatement? _scanPs;

    // Prepared statements for variable field sets, keyed by the ordered field list
    // (ScyllaDbHelper.BuildFieldsKey). Task values make concurrent first-use prepare only once.
    private readonly ConcurrentDictionary<string, Task<PreparedStatement>> _updatePsCache = new();
    private readonly ConcurrentDictionary<string, Task<PreparedStatement>> _projectedReadPsCache = new();
    private readonly ConcurrentDictionary<string, Task<PreparedStatement>> _projectedScanPsCache = new();

    private readonly int _replicationFactor;

    public ScyllaYcsbClient(Dictionary<string, string> props)
    {
        try
        {
            var hosts = YcsbCliArgs.TryGet(props, "scylladb.hosts", defaultValue: "localhost")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var port = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "scylladb.port", defaultValue: "9042"), 9042);
            _keyspace = YcsbCliArgs.TryGet(props, "scylladb.keyspace", defaultValue: "ycsb");
            var user = YcsbCliArgs.TryGet(props, "scylladb.user", defaultValue: "");
            var password = YcsbCliArgs.TryGet(props, "scylladb.password", defaultValue: "");

            _fieldCount = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "fieldcount", defaultValue: "10"), 10);
            _fieldNames = [.. Enumerable.Range(1, _fieldCount).Select(i => $"field{i}")];

            _replicationFactor = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "scylladb.replicationfactor", defaultValue: "3"), 3);
            if (_replicationFactor < 1)
                throw new ArgumentOutOfRangeException(nameof(props), _replicationFactor, "scylladb.replicationfactor must be >= 1");

            var builder = Cluster.Builder()
                .AddContactPoints(hosts)
                .WithPort(port);

            if (!string.IsNullOrEmpty(user))
                builder = builder.WithCredentials(user, password);

            _session = builder.Build().Connect();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to ScyllaDB: {ex.Message}");
            throw;
        }
    }

    public async Task<Response<object>> InitDb()
    {
        // Note: IF NOT EXISTS does not alter the replication of an already existing keyspace.
        await _session.ExecuteAsync(new SimpleStatement(
            ScyllaDbHelper.BuildCreateKeyspaceCql(_keyspace, _replicationFactor)
        ));

        // Drop and recreate so the column set always matches the current fieldcount.
        // IF NOT EXISTS would silently keep a stale schema from a previous run.
        await _session.ExecuteAsync(new SimpleStatement(
            $"DROP TABLE IF EXISTS {_keyspace}.{TABLE_NAME}"
        ));

        // The table was dropped, so previously prepared statements are stale.
        _updatePsCache.Clear();
        _projectedReadPsCache.Clear();
        _projectedScanPsCache.Clear();

        var columnDefs = string.Join(", ", _fieldNames.Select(f => $"{f} text"));
        await _session.ExecuteAsync(new SimpleStatement(
            $"CREATE TABLE {_keyspace}.{TABLE_NAME} " +
            $"({PRIMARY_KEY} text PRIMARY KEY, {columnDefs})"
        ));

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
        await _session.ExecuteAsync((_insertPs ?? await PrepareInsert()).Bind(ScyllaDbHelper.BuildInsertBindings(key, _fieldNames, values)));
        return Response.Ok(sizeBytes: ScyllaDbHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        // YCSB sends only the subset of fields to modify. The statement sets just those columns,
        // so other columns remain untouched — no prior read needed.
        await ExecuteUpdate(key, values);

        return Response.Ok(sizeBytes: ScyllaDbHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        if (fields == null || fields.Count == 0)
        {
            var rowSet = await _session.ExecuteAsync((_readPs ?? await PrepareRead()).Bind(key));
            var row = rowSet.FirstOrDefault();

            if (row == null) return Response.Fail(statusCode: "no data");

            var result = ScyllaDbHelper.ExtractAllFields(row, _fieldNames, columnOffset: 0);

            return Response.Ok(sizeBytes: ScyllaDbHelper.GetSize(key, result));
        }
        else
        {
            var fieldList = fields.ToList();
            var ps = await GetProjectedReadPs(fieldList);
            var rowSet = await _session.ExecuteAsync(ps.Bind(key));
            var row = rowSet.FirstOrDefault();
            if (row == null) return Response.Fail(statusCode: "no data");

            var result = ScyllaDbHelper.ExtractFields(row, fieldList, columnOffset: 0);
            return Response.Ok(sizeBytes: ScyllaDbHelper.GetSize(key, result));
        }
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        // YCSB workload F: read the record, then write it back. Size covers both phases.
        long size;

        if (fields == null || fields.Count == 0)
        {
            var rowSet = await _session.ExecuteAsync((_readPs ?? await PrepareRead()).Bind(key));
            var row = rowSet.FirstOrDefault();
            if (row == null) return Response.Fail(statusCode: "no data");

            size = ScyllaDbHelper.GetSize(key, ScyllaDbHelper.ExtractAllFields(row, _fieldNames, columnOffset: 0));
        }
        else
        {
            var fieldList = fields.ToList();

            var ps = await GetProjectedReadPs(fieldList);
            var rowSet = await _session.ExecuteAsync(ps.Bind(key));
            var row = rowSet.FirstOrDefault();
            if (row == null) return Response.Fail(statusCode: "no data");

            size = ScyllaDbHelper.GetSize(key, ScyllaDbHelper.ExtractFields(row, fieldList, columnOffset: 0));
        }

        await ExecuteUpdate(key, values);

        size += ScyllaDbHelper.GetSize(key, values);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields)
    {
        // Materialize the requested fields once. Used both to build the query and
        // to extract columns below, so it must stay consistent.
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
                ? ScyllaDbHelper.ExtractAllFields(row, _fieldNames, columnOffset: 1)
                : ScyllaDbHelper.ExtractFields(row, fieldList, columnOffset: 1);

            sizeBytes += ScyllaDbHelper.GetSize(k, result);
        }

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        using var semaphore = new SemaphoreSlim(_maxConcurrency);

        var tasks = data.Select(async item =>
        {
            await semaphore.WaitAsync();

            try
            {
                await _session.ExecuteAsync((_insertPs ?? await PrepareInsert()).Bind(ScyllaDbHelper.BuildInsertBindings(item.Key, _fieldNames, item.Value)));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return Response.Ok();
    }

    // InitDb prepares these eagerly; the ?? fallbacks at the call sites only matter when
    // the table already exists and InitDb was not called on this client instance.
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

        // Scylla can't range-scan a partition key directly, so we page by
        // TOKEN(key). Note: rows come back in token (hash) order, not key order.
        return _scanPs = await _session.PrepareAsync(
            $"SELECT {PRIMARY_KEY}, {fieldCols} FROM {_keyspace}.{TABLE_NAME} " +
            $"WHERE TOKEN({PRIMARY_KEY}) >= TOKEN(?) LIMIT ?"
        );
    }

    private async Task ExecuteUpdate(string key, Dictionary<string, string> values)
    {
        // Snapshot the key order once so the prepared statement and the bindings agree.
        var fieldList = values.Keys.ToList();
        var ps = await GetUpdatePs(fieldList);
        await _session.ExecuteAsync(ps.Bind(ScyllaDbHelper.BuildUpdateBindings(key, fieldList, values)));
    }

    private Task<PreparedStatement> GetUpdatePs(IReadOnlyList<string> fields)
        => GetOrPrepare(_updatePsCache, fields, ScyllaDbHelper.BuildUpdateCql);

    private Task<PreparedStatement> GetProjectedReadPs(IReadOnlyList<string> fields)
        => GetOrPrepare(_projectedReadPsCache, fields, ScyllaDbHelper.BuildProjectedReadCql);

    private Task<PreparedStatement> GetProjectedScanPs(IReadOnlyList<string> fields)
        => GetOrPrepare(_projectedScanPsCache, fields, ScyllaDbHelper.BuildProjectedScanCql);

    private async Task<PreparedStatement> GetOrPrepare(
        ConcurrentDictionary<string, Task<PreparedStatement>> cache, IReadOnlyList<string> fields,
        Func<IReadOnlyList<string>, string, string, string, string> buildCql)
    {
        var cacheKey = ScyllaDbHelper.BuildFieldsKey(fields);
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
        // TRUNCATE wipes the table between runs. Relies on InitDb having created it first.
        await _session.ExecuteAsync(new SimpleStatement(
            $"TRUNCATE TABLE {_keyspace}.{TABLE_NAME}"
        ));

        return Response.Ok();
    }
}
