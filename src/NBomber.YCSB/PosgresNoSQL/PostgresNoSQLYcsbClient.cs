using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.YCSB.DAL;
using NBomber.YCSB.Infra;
using Npgsql;
using Spectre.Console;
using System.Text;
using System.Text.Json;

namespace NBomber.YCSB.PosgresNoSQL;

public class PostgresNoSQLYcsbClient : IDbYcsbClient
{
    private static NpgsqlDataSource? _dataSource;

    public const string PRIMARY_KEY = "ycsb_key";
    public const string COLUMN_NAME = "ycsb_value";
    public const string TABLE_NAME = "ycsb_table";

    public PostgresNoSQLYcsbClient(Dictionary<string, string> props)
    {
        var host = YcsbCliArgs.TryGet(props, "postgres.host", defaultValue: "localhost");
        var port = YcsbCliArgs.TryGet(props, "postgres.port", defaultValue: "5432");
        var database = YcsbCliArgs.TryGet(props, "postgres.database", defaultValue: "test");
        var user = YcsbCliArgs.TryGet(props, "postgres.user", defaultValue: "postgres");
        var password = YcsbCliArgs.TryGet(props, "postgres.password", "postgres");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port),
            Database = database,
            Username = user,
            Password = password,
            MinPoolSize = 1,
            MaxPoolSize = 100
        };

        _dataSource = NpgsqlDataSource.Create(builder);
    }

    public async Task<Response<object>> InitDb()
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync();

            string checkTableSql = $@"
                    SELECT EXISTS (
                        SELECT FROM information_schema.tables
                        WHERE table_schema = 'public'
                        AND table_name = '{TABLE_NAME}'
                    )";

            using var checkCmd = new NpgsqlCommand(checkTableSql, conn);

            bool exists = (bool)await checkCmd.ExecuteScalarAsync();

            if (!exists)
            {
                string createTableAndIndexSql = $@"
                    CREATE TABLE {TABLE_NAME} (
                        {PRIMARY_KEY} VARCHAR(255) PRIMARY KEY NOT NULL,
                        {COLUMN_NAME} JSONB NOT NULL
                    );

                    CREATE INDEX idx_{TABLE_NAME}_jsonb
                    ON {TABLE_NAME} USING GIN ({COLUMN_NAME});";

                using var cmd = new NpgsqlCommand(createTableAndIndexSql, conn);
                
                await cmd.ExecuteNonQueryAsync();
            }

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

        var sizeBytes = Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(jsonValue);

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> Read(string table, string key, HashSet<string>? fields)
    {
        string sql;

        if (fields == null || fields.Count == 0)
        {
            sql = $@"
                SELECT {COLUMN_NAME}
                FROM {TABLE_NAME}
                WHERE {PRIMARY_KEY} = @key";
        }
        else
        {
            var fieldSelectors = string.Join(", ",
                fields.Select(f => $"'{f}', {COLUMN_NAME}->'{f}'"));

            sql = $@"
                SELECT jsonb_build_object({fieldSelectors}) as {COLUMN_NAME}
                FROM {TABLE_NAME}
                WHERE {PRIMARY_KEY} = @key";
        }

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            return Response.Fail();
        }

        string jsonValue = result.ToString();
        var sizeBytes = Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(jsonValue);

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields)
    {
        string sql;

        if (fields == null || fields.Count == 0)
        {
            sql = $@"
                SELECT {PRIMARY_KEY}, {COLUMN_NAME}
                FROM {TABLE_NAME}
                WHERE {PRIMARY_KEY} >= @startKey
                ORDER BY {PRIMARY_KEY}
                LIMIT @count";
        }
        else
        {
            var fieldSelectors = string.Join(", ",
                fields.Select(f => $"'{f}', {COLUMN_NAME}->'{f}'"));

            sql = $@"
                SELECT {PRIMARY_KEY}, jsonb_build_object({fieldSelectors}) as {COLUMN_NAME}
                FROM {TABLE_NAME}
                WHERE {PRIMARY_KEY} >= @startKey
                ORDER BY {PRIMARY_KEY}
                LIMIT @count";
        }

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@startKey", startKey);
        cmd.Parameters.AddWithValue("@count", count);

        var sizeBytes = 0L;

        using var reader = await cmd.ExecuteReaderAsync();
     
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var jsonValue = reader.GetValue(1)?.ToString() ?? "";

            sizeBytes += Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(jsonValue);
        }

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> Update(string table, string key, Dictionary<string, string> values)
    {
        string jsonValue = JsonSerializer.Serialize(values);

        string sql = $@"
            UPDATE {TABLE_NAME}
            SET {COLUMN_NAME} = @json::jsonb
            WHERE {PRIMARY_KEY} = @key";

        await using var conn = await _dataSource.OpenConnectionAsync();

        using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@json", jsonValue);

        await cmd.ExecuteNonQueryAsync();

        var sizeBytes = Encoding.UTF8.GetByteCount(key) + Encoding.UTF8.GetByteCount(jsonValue);

        return Response.Ok(sizeBytes: sizeBytes);
    }

    public async Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data)
    {
        try
        {
            const int chunkSize = 1000;

            var chunks = data.Chunk(chunkSize).ToList();

            var tasks = chunks.Select(async chunk =>
            {
                // Each task gets its own connection from the pool
                await using var conn = await _dataSource.OpenConnectionAsync();

                var batch = new NpgsqlBatch(conn);

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
                batch.Dispose();
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
            string sql = $"TRUNCATE TABLE {TABLE_NAME}";

            await using var conn = await _dataSource.OpenConnectionAsync();

            using var cmd = new NpgsqlCommand(sql, conn);

            await cmd.ExecuteNonQueryAsync();

            return Response.Ok();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during delete all data: {ex.Message}");
            return Response.Fail();
        }
    }
}

