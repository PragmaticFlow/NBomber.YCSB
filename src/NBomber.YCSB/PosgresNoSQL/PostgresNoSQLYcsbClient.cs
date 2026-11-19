using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using Npgsql;
using System.Text.Json;

namespace NBomber.YCSB.PosgresNoSQL;

public class PostgresNoSQLYcsbClient : IDbYcsbClient
{
    private static NpgsqlDataSource? _dataSource;

    public const string PRIMARY_KEY = "ycsb_key";
    public const string COLUMN_NAME = "ycsb_value";
    public const string TABLE_NAME = "ycsb_table";

    private const string TABLE_COLUMNS = $"{PRIMARY_KEY} VARCHAR(255) PRIMARY KEY NOT NULL, {COLUMN_NAME} JSONB NOT NULL";

    public PostgresNoSQLYcsbClient(Dictionary<string, string> props)
    {
        _dataSource = NpgsqlDataSource.Create(PostgresNoSQLHelper.BuildConnectionString(props));
    }

    public async Task<Response<object>> InitDb()
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            using var cmd = new NpgsqlCommand($"CREATE TABLE IF NOT EXISTS {TABLE_NAME} ({TABLE_COLUMNS})", conn);

            await cmd.ExecuteNonQueryAsync();

            return Response.Ok();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during initialization db: {ex.Message}");
            throw;
        }
    }

    public async Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values)
    {
        string jsonValue = JsonSerializer.Serialize(values);

        string sql = $@"
            INSERT INTO {TABLE_NAME} ({PRIMARY_KEY}, {COLUMN_NAME})
            VALUES (@key, @json::jsonb)";

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@json", jsonValue);

        await cmd.ExecuteNonQueryAsync();

        var sizeBytes = PostgresNoSQLHelper.GetSize(key, jsonValue);

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = CreateReadCommand(conn, key, fields);

        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            return Response.Fail(statusCode: "no data");
        }

        string jsonValue = result.ToString();
        var sizeBytes = PostgresNoSQLHelper.GetSize(key, jsonValue);

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(null, conn);

        string selected = fields == null || fields.Count == 0
            ? COLUMN_NAME
            : $"{PostgresNoSQLHelper.BuildProjection(fields, cmd.Parameters)} as {COLUMN_NAME}";

        cmd.CommandText = $@"
                SELECT {PRIMARY_KEY}, {selected}
                FROM {TABLE_NAME}
                WHERE {PRIMARY_KEY} >= @startKey
                ORDER BY {PRIMARY_KEY}
                LIMIT @count";

        cmd.Parameters.AddWithValue("@startKey", startKey);
        cmd.Parameters.AddWithValue("@count", count);

        var sizeBytes = 0L;

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var jsonValue = reader.GetValue(1)?.ToString() ?? "";

            sizeBytes += PostgresNoSQLHelper.GetSize(key, jsonValue);
        }

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values)
    {
        await using var conn = await _dataSource.OpenConnectionAsync();

        using var readCmd = CreateReadCommand(conn, key, fields);

        var result = await readCmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
            return Response.Fail(statusCode: "no data");

        string readJson = result.ToString();
        long size = PostgresNoSQLHelper.GetSize(key, readJson);

        string updateJson = JsonSerializer.Serialize(values);

        using var updateCmd = CreateUpdateCommand(conn, key, updateJson);
        var rows = await updateCmd.ExecuteNonQueryAsync();

        if (rows == 0)
            return Response.Fail(statusCode: "no data");

        size += PostgresNoSQLHelper.GetSize(key, updateJson);

        return Response.Ok(sizeBytes: size);
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        string jsonValue = JsonSerializer.Serialize(values);

        await using var conn = await _dataSource.OpenConnectionAsync();
        using var cmd = CreateUpdateCommand(conn, key, jsonValue);

        var rows = await cmd.ExecuteNonQueryAsync();

        if (rows == 0)
            return Response.Fail(statusCode: "no data");

        var sizeBytes = PostgresNoSQLHelper.GetSize(key, jsonValue);

        return Response.Ok(sizeBytes: sizeBytes);
    }

    private static NpgsqlCommand CreateReadCommand(NpgsqlConnection conn, string key, HashSet<string>? fields)
    {
        var cmd = new NpgsqlCommand(null, conn);
        var selected = fields == null || fields.Count == 0
            ? COLUMN_NAME
            : $"{PostgresNoSQLHelper.BuildProjection(fields, cmd.Parameters)} as {COLUMN_NAME}";

        cmd.CommandText = $@"
                SELECT {selected}
                FROM {TABLE_NAME}
                WHERE {PRIMARY_KEY} = @key";
        cmd.Parameters.AddWithValue("@key", key);
        return cmd;
    }

    private static NpgsqlCommand CreateUpdateCommand(NpgsqlConnection conn, string key, string jsonValue)
    {
        const string sql = $@"
            UPDATE {TABLE_NAME}
            SET {COLUMN_NAME} = {COLUMN_NAME} || @json::jsonb
            WHERE {PRIMARY_KEY} = @key";

        var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@json", jsonValue);
        return cmd;
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        try
        {
            const int chunkSize = 1000;

            var tasks = data.Chunk(chunkSize).Select(async chunk =>
            {
                await using var conn = await _dataSource.OpenConnectionAsync();

                using var batch = new NpgsqlBatch(conn);

                foreach (var item in chunk)
                {
                    var jsonValue = JsonSerializer.Serialize(item.Value);

                    var batchCommand = new NpgsqlBatchCommand(
                        $"INSERT INTO {TABLE_NAME} ({PRIMARY_KEY}, {COLUMN_NAME}) VALUES ($1, $2::jsonb) ON CONFLICT ({PRIMARY_KEY}) DO NOTHING"
                    );

                    batchCommand.Parameters.AddWithValue(item.Key);
                    batchCommand.Parameters.AddWithValue(jsonValue);

                    batch.BatchCommands.Add(batchCommand);
                }

                await batch.ExecuteNonQueryAsync();
            });

            await Task.WhenAll(tasks);

            return Response.Ok();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during bulk insert: {ex.Message}");
            throw;
        }
    }

    public async Task<Response<object>> DeleteAllData()
    {
        try
        {
            string sql = $"DROP TABLE IF EXISTS {TABLE_NAME}";

            await using var conn = await _dataSource.OpenConnectionAsync();

            using var cmd = new NpgsqlCommand(sql, conn);

            await cmd.ExecuteNonQueryAsync();

            return Response.Ok();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during delete all data: {ex.Message}");
            throw;
        }
    }
}

