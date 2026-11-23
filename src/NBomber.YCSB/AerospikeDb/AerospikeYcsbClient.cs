using Aerospike.Client;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.AerospikeDb;

public class AerospikeYcsbClient : IDbYcsbClient, IDisposable
{
    private readonly AsyncClient _client;
    private readonly int _maxConcurrency;
    private readonly string _namespace;
    private readonly string _set;

    private readonly Policy _readPolicy;
    private readonly WritePolicy _insertPolicy;
    private readonly WritePolicy _updatePolicy;
    private readonly WritePolicy _bulkPolicy;

    public AerospikeYcsbClient(Dictionary<string, string> props)
    {
        _maxConcurrency = AerospikeHelper.GetMaxConcurrency(props);

        try
        {
            var host = YcsbCliArgs.TryGet(props, "aerospike.host", defaultValue: "localhost");
            var port = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "aerospike.port", defaultValue: "3000"), 3000);
            _namespace = YcsbCliArgs.TryGet(props, "aerospike.namespace", defaultValue: "test");
            _set = YcsbCliArgs.TryGet(props, "aerospike.set", defaultValue: "ycsb_table");
            var timeout = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "aerospike.timeout", defaultValue: "10000"), 10000);

            // asyncMaxConnsPerNode defaults to the sync maxConnsPerNode (100), which would fail commands above 100.
            var clientPolicy = new AsyncClientPolicy
            {
                timeout = timeout,
                asyncMaxCommands = _maxConcurrency,
                asyncMaxCommandAction = MaxCommandAction.DELAY,
                asyncMaxConnsPerNode = _maxConcurrency,
            };

            _readPolicy = new Policy { totalTimeout = timeout, maxRetries = 0 };
            _insertPolicy = new WritePolicy { totalTimeout = timeout, recordExistsAction = RecordExistsAction.CREATE_ONLY };
            _updatePolicy = new WritePolicy { totalTimeout = timeout, recordExistsAction = RecordExistsAction.UPDATE_ONLY };
            _bulkPolicy = new WritePolicy { totalTimeout = timeout, recordExistsAction = RecordExistsAction.REPLACE };

            _client = new AsyncClient(clientPolicy, host, port);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to connect to Aerospike: {ex.Message}");
            throw;
        }
    }

    public Task<Response<object>> InitDb()
    {
        EnsureNamespaceExists();

        return Task.FromResult(Response.Ok());
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        await _client.Put(_insertPolicy, CancellationToken.None, new Key(_namespace, _set, key), AerospikeHelper.BuildBins(values));

        return Response.Ok(sizeBytes: AerospikeHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        if (!await Update(key, values))
            return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: AerospikeHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        var record = await Get(key, fields);

        return record == null
            ? Response.Fail(statusCode: "no data")
            : Response.Ok(sizeBytes: AerospikeHelper.GetSize(key, record));
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        var record = await Get(key, fields);
        if (record == null) return Response.Fail(statusCode: "no data");

        if (!await Update(key, values))
            return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: AerospikeHelper.GetSize(key, record) + AerospikeHelper.GetSize(key, values));
    }

    // Aerospike has no ordered range scan over primary keys (only unordered set scans),
    // so scan is not supported rather than returning keys in the wrong order.
    public Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields)
        => Task.FromResult(Response.Fail(statusCode: "scan not supported"));

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = _maxConcurrency };

        await Parallel.ForEachAsync(data, options, async (item, ct) =>
            await _client.Put(_bulkPolicy, ct, new Key(_namespace, _set, item.Key), AerospikeHelper.BuildBins(item.Value)));

        return Response.Ok();
    }

    public void Dispose() => _client.Close();

    public Task<Response<object>> DeleteAllData()
    {
        // DeleteAllData runs before InitDb, so the namespace must be checked here too.
        EnsureNamespaceExists();

        // Server-side truncate of this set only; records written after the call survive.
        _client.Truncate(null, _namespace, _set, null);

        return Task.FromResult(Response.Ok());
    }

    private void EnsureNamespaceExists()
    {
        var namespaces = Info.Request(_client.Nodes[0], "namespaces");

        if (!AerospikeHelper.HasNamespace(namespaces, _namespace))
            throw new InvalidOperationException(
                $"Aerospike namespace '{_namespace}' does not exist (server has: {namespaces}). Set aerospike.namespace.");
    }

    private Task<Record> Get(string key, HashSet<string>? fields)
    {
        var aerospikeKey = new Key(_namespace, _set, key);

        return fields == null || fields.Count == 0
            ? _client.Get(_readPolicy, CancellationToken.None, aerospikeKey)
            : _client.Get(_readPolicy, CancellationToken.None, aerospikeKey, fields.ToArray());
    }

    private async Task<bool> Update(string key, Dictionary<string, string> values)
    {
        try
        {
            await _client.Put(_updatePolicy, CancellationToken.None, new Key(_namespace, _set, key), AerospikeHelper.BuildBins(values));
            return true;
        }
        catch (AerospikeException ex) when (ex.Result == ResultCode.KEY_NOT_FOUND_ERROR)
        {
            return false;
        }
    }
}
