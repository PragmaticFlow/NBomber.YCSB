using Cassandra;
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
        await _session.ExecuteAsync(new SimpleStatement(
            $"CREATE KEYSPACE IF NOT EXISTS {_keyspace} " +
            $"WITH replication = {{'class': 'SimpleStrategy', 'replication_factor': 3}}"
        ));

        // Drop and recreate so the column set always matches the current fieldcount.
        // IF NOT EXISTS would silently keep a stale schema from a previous run.
        await _session.ExecuteAsync(new SimpleStatement(
            $"DROP TABLE IF EXISTS {_keyspace}.{TABLE_NAME}"
        ));

        var columnDefs = string.Join(", ", _fieldNames.Select(f => $"{f} text"));
        await _session.ExecuteAsync(new SimpleStatement(
            $"CREATE TABLE {_keyspace}.{TABLE_NAME} " +
            $"({PRIMARY_KEY} text PRIMARY KEY, {columnDefs})"
        ));

        var fieldCols = string.Join(", ", _fieldNames);
        var fieldPlaceholders = string.Join(", ", _fieldNames.Select(_ => "?"));

        _insertPs = await _session.PrepareAsync(
            $"INSERT INTO {_keyspace}.{TABLE_NAME} ({PRIMARY_KEY}, {fieldCols}) VALUES (?, {fieldPlaceholders})"
        );

        _readPs = await _session.PrepareAsync(
            $"SELECT {fieldCols} FROM {_keyspace}.{TABLE_NAME} WHERE {PRIMARY_KEY} = ?"
        );

        // Scylla can't range-scan a partition key directly, so we page by
        // TOKEN(key). Note: rows come back in token (hash) order, not key order.
        _scanPs = await _session.PrepareAsync(
            $"SELECT {PRIMARY_KEY}, {fieldCols} FROM {_keyspace}.{TABLE_NAME} " +
            $"WHERE TOKEN({PRIMARY_KEY}) >= TOKEN(?) LIMIT ?"
        );

        return Response.Ok();
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        await _session.ExecuteAsync(_insertPs!.Bind(ScyllaDbHelper.BuildInsertBindings(key, _fieldNames, values)));
        return Response.Ok(sizeBytes: ScyllaDbHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        // UPDATE is built dynamically because YCSB sends only the subset of fields to modify.
        // Other columns remain untouched — no prior read needed.
        var setClauses = string.Join(", ", values.Keys.Select(f => $"{f} = ?"));
        var args = values.Values.Cast<object>().Append(key).ToArray();

        await _session.ExecuteAsync(new SimpleStatement(
            $"UPDATE {_keyspace}.{TABLE_NAME} SET {setClauses} WHERE {PRIMARY_KEY} = ?", args));

        return Response.Ok(sizeBytes: ScyllaDbHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        if (fields == null || fields.Count == 0)
        {
            var rowSet = await _session.ExecuteAsync(_readPs!.Bind(key));
            var row = rowSet.FirstOrDefault();

            if (row == null) return Response.Fail(statusCode: "no data");

            var result = ScyllaDbHelper.ExtractAllFields(row, _fieldNames, columnOffset: 0);

            return Response.Ok(sizeBytes: ScyllaDbHelper.GetSize(key, result));
        }
        else
        {
            var fieldList = fields.ToList();
            var rowSet = await _session.ExecuteAsync(ScyllaDbHelper.BuildProjectedRead(key, fieldList, _keyspace, TABLE_NAME, PRIMARY_KEY));
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
            var rowSet = await _session.ExecuteAsync(_readPs!.Bind(key));
            var row = rowSet.FirstOrDefault();
            if (row == null) return Response.Fail(statusCode: "no data");

            size = ScyllaDbHelper.GetSize(key, ScyllaDbHelper.ExtractAllFields(row, _fieldNames, columnOffset: 0));
        }
        else
        {
            var fieldList = fields.ToList();

            var rowSet = await _session.ExecuteAsync(ScyllaDbHelper.BuildProjectedRead(key, fieldList, _keyspace, TABLE_NAME, PRIMARY_KEY));
            var row = rowSet.FirstOrDefault();
            if (row == null) return Response.Fail(statusCode: "no data");

            size = ScyllaDbHelper.GetSize(key, ScyllaDbHelper.ExtractFields(row, fieldList, columnOffset: 0));
        }

        var setClauses = string.Join(", ", values.Keys.Select(f => $"{f} = ?"));
        var args = values.Values.Cast<object>().Append(key).ToArray();

        await _session.ExecuteAsync(new SimpleStatement(
            $"UPDATE {_keyspace}.{TABLE_NAME} SET {setClauses} WHERE {PRIMARY_KEY} = ?", args));

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
            rowSet = await _session.ExecuteAsync(_scanPs!.Bind(startKey, count));
        }
        else
        {
            var cols = string.Join(", ", fieldList);
            rowSet = await _session.ExecuteAsync(new SimpleStatement(
                $"SELECT {PRIMARY_KEY}, {cols} FROM {_keyspace}.{TABLE_NAME} " +
                $"WHERE TOKEN({PRIMARY_KEY}) >= TOKEN(?) LIMIT ?",
                startKey, count));
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
                await _session.ExecuteAsync(_insertPs!.Bind(ScyllaDbHelper.BuildInsertBindings(item.Key, _fieldNames, item.Value)));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        return Response.Ok();
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
