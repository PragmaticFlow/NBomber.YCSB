using MySqlConnector;
using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;

namespace NBomber.YCSB.TiDb;

public class TiDbYcsbClient : IDbYcsbClient
{
    // Rows per multi-row INSERT statement in BulkInsert (unmeasured init load).
    private const int BULK_CHUNK_SIZE = 500;
    // Max concurrent bulk INSERT statements, so the load does not overload the small TiDB/TiKV test stack.
    private const int BULK_PARALLELISM = 8;

    private readonly MySqlDataSource _dataSource;
    private readonly string _database;
    private readonly string[] _fieldNames;

    private readonly string _insertSql;
    private readonly string _readAllSql;
    private readonly string _scanAllSql;

    public TiDbYcsbClient(Dictionary<string, string> props)
    {
        _database = TiDbHelper.GetDatabase(props);

        var fieldCount = YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "fieldcount", defaultValue: "10"), 10);
        _fieldNames = [.. Enumerable.Range(1, fieldCount).Select(i => $"field{i}")];

        _insertSql = TiDbHelper.BuildInsertSql(_database, _fieldNames, rowCount: 1);
        _readAllSql = TiDbHelper.BuildReadSql(_database, _fieldNames);
        _scanAllSql = TiDbHelper.BuildScanSql(_database, _fieldNames);

        _dataSource = new MySqlDataSource(TiDbHelper.BuildConnectionString(props));
    }

    public async Task<Response<object>> InitDb()
    {
        await ExecuteNonQuery(TiDbHelper.BuildCreateDatabaseSql(_database));
        await ExecuteNonQuery(TiDbHelper.BuildCreateTableSql(_database, _fieldNames));

        return Response.Ok();
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(_insertSql, conn);

        AddInsertParameters(cmd, key, values);
        await cmd.ExecuteNonQueryAsync();

        return Response.Ok(sizeBytes: TiDbHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        if (await ExecuteUpdate(key, values) == 0)
            return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: TiDbHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var size = await ReadSize(conn, key, fields);
        if (size < 0) return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        var size = await ReadSize(conn, key, fields);
        if (size < 0) return Response.Fail(statusCode: "no data");

        if (await ExecuteUpdate(conn, key, values) == 0)
            return Response.Fail(statusCode: "no data");

        return Response.Ok(sizeBytes: size + TiDbHelper.GetSize(key, values));
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields)
    {
        var projected = fields != null && fields.Count > 0;
        IEnumerable<string> columns = projected ? fields! : _fieldNames;

        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(projected ? TiDbHelper.BuildScanSql(_database, columns) : _scanAllSql, conn);

        AddParameter(cmd, startKey);
        AddParameter(cmd, count);

        await using var reader = await cmd.ExecuteReaderAsync();

        long sizeBytes = 0;
        while (await reader.ReadAsync())
        {
            // Column 0 is the primary key; field columns start at offset 1.
            sizeBytes += TiDbHelper.GetRowSize(reader.GetString(0), columns, reader, offset: 1);
        }

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        await Parallel.ForEachAsync(
            data.Chunk(BULK_CHUNK_SIZE),
            new ParallelOptions { MaxDegreeOfParallelism = BULK_PARALLELISM },
            async (chunk, _) =>
            {
                await using var conn = await _dataSource.OpenConnectionAsync();
                await using var cmd = new MySqlCommand(
                    chunk.Length == 1 ? _insertSql : TiDbHelper.BuildInsertSql(_database, _fieldNames, chunk.Length), conn);

                foreach (var item in chunk)
                    AddInsertParameters(cmd, item.Key, item.Value);

                await cmd.ExecuteNonQueryAsync();
            });

        return Response.Ok();
    }

    public async Task<Response<object>> DeleteAllData()
    {
        await ExecuteNonQuery(TiDbHelper.BuildDropTableSql(_database));

        return Response.Ok();
    }

    // Returns the key + field bytes of the row, or -1 when there is no such row.
    private async Task<long> ReadSize(MySqlConnection conn, string key, HashSet<string>? fields)
    {
        var projected = fields != null && fields.Count > 0;
        IEnumerable<string> columns = projected ? fields! : _fieldNames;

        await using var cmd = new MySqlCommand(projected ? TiDbHelper.BuildReadSql(_database, columns) : _readAllSql, conn);
        AddParameter(cmd, key);

        await using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync() ? TiDbHelper.GetRowSize(key, columns, reader, offset: 0) : -1;
    }

    private async Task<int> ExecuteUpdate(string key, Dictionary<string, string> values)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        return await ExecuteUpdate(conn, key, values);
    }

    // The default (found rows) semantics report a matched row even if the new values equal the old ones,
    // so 0 means the key does not exist.
    private async Task<int> ExecuteUpdate(MySqlConnection conn, string key, Dictionary<string, string> values)
    {
        await using var cmd = new MySqlCommand(TiDbHelper.BuildUpdateSql(_database, values.Keys), conn);

        foreach (var value in values.Values)
            AddParameter(cmd, value);

        AddParameter(cmd, key);

        return await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExecuteNonQuery(string sql)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(sql, conn);

        await cmd.ExecuteNonQueryAsync();
    }

    private void AddInsertParameters(MySqlCommand cmd, string key, Dictionary<string, string> values)
    {
        AddParameter(cmd, key);

        var matched = 0;
        foreach (var name in _fieldNames)
        {
            var found = values.TryGetValue(name, out var value);
            if (found) matched++;

            AddParameter(cmd, found ? value : null);
        }

        // A fieldcount mismatch would otherwise silently drop the extra fields.
        if (matched != values.Count)
            throw new ArgumentException($"Record '{key}' has fields that are not among the {_fieldNames.Length} configured fieldcount fields.", nameof(values));
    }

    private static void AddParameter(MySqlCommand cmd, object? value)
        => cmd.Parameters.Add(new MySqlParameter { Value = value ?? DBNull.Value });
}
