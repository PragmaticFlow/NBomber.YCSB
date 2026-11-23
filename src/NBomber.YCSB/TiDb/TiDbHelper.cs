using MySqlConnector;
using NBomber.YCSB.Infra;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace NBomber.YCSB.TiDb;

public static class TiDbHelper
{
    public const string TABLE_NAME = "ycsb_table";
    public const string PRIMARY_KEY = "ycsb_key";

    private static readonly Regex DATABASE_NAME = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static string BuildConnectionString(Dictionary<string, string> props)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = YcsbCliArgs.TryGet(props, "tidb.host", defaultValue: "localhost"),
            Port = (uint)YcsbCliArgs.TryParseInt(YcsbCliArgs.TryGet(props, "tidb.port", defaultValue: "4000"), 4000),
            UserID = YcsbCliArgs.TryGet(props, "tidb.user", defaultValue: "root"),
            Password = YcsbCliArgs.TryGet(props, "tidb.password", defaultValue: ""),
            MinimumPoolSize = 1,
            MaximumPoolSize = 100,
            // No session state is changed, so the extra reset round trip on every pooled open is not needed.
            ConnectionReset = false
        };

        return builder.ConnectionString;
    }

    public static string GetDatabase(Dictionary<string, string> props)
    {
        var database = YcsbCliArgs.TryGet(props, "tidb.database", defaultValue: "ycsb");

        // The name is interpolated into SQL, so only plain identifiers are accepted.
        if (!DATABASE_NAME.IsMatch(database))
            throw new ArgumentException($"tidb.database must match ^[A-Za-z0-9_]+$, got '{database}'", nameof(props));

        return database;
    }

    public static long GetSize(string key, IDictionary<string, string>? fields)
    {
        long size = Encoding.UTF8.GetByteCount(key);

        if (fields != null)
        {
            foreach (var kv in fields)
                size += Encoding.UTF8.GetByteCount(kv.Key) + Encoding.UTF8.GetByteCount(kv.Value ?? "");
        }

        return size;
    }

    // Sums key + (column name + value) for the non-null columns of the current row, starting at `offset`.
    // Same accounting as GetSize on the dictionary the other clients build.
    public static long GetRowSize(string key, IEnumerable<string> columnNames, DbDataReader row, int offset)
    {
        long size = Encoding.UTF8.GetByteCount(key);
        var column = offset;

        foreach (var name in columnNames)
        {
            if (!row.IsDBNull(column))
                size += Encoding.UTF8.GetByteCount(name) + Encoding.UTF8.GetByteCount(row.GetString(column));

            column++;
        }

        return size;
    }

    public static string BuildCreateDatabaseSql(string database)
        => $"CREATE DATABASE IF NOT EXISTS {database}";

    // Binary collation so ORDER BY / range scans follow byte order, like the other SQL backends' default keys.
    // CLUSTERED stores rows in primary key order (row key = ycsb_key), the Java JDBC schema shape.
    public static string BuildCreateTableSql(string database, IEnumerable<string> fieldNames)
        => $"CREATE TABLE IF NOT EXISTS {database}.{TABLE_NAME} " +
           $"({PRIMARY_KEY} VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, " +
           $"{string.Join(", ", fieldNames.Select(f => $"{f} TEXT"))}, " +
           $"PRIMARY KEY ({PRIMARY_KEY}) CLUSTERED) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin";

    public static string BuildDropTableSql(string database)
        => $"DROP TABLE IF EXISTS {database}.{TABLE_NAME}";

    // Placeholders are positional: [key, field_1, ..., field_N] per row.
    public static string BuildInsertSql(string database, IReadOnlyList<string> fieldNames, int rowCount)
    {
        var row = "(?" + string.Concat(Enumerable.Repeat(", ?", fieldNames.Count)) + ")";
        var sql = new StringBuilder($"INSERT INTO {database}.{TABLE_NAME} ({PRIMARY_KEY}, {string.Join(", ", fieldNames)}) VALUES ", capacity: 64 + rowCount * (row.Length + 2));

        for (var i = 0; i < rowCount; i++)
        {
            if (i > 0) sql.Append(", ");
            sql.Append(row);
        }

        return sql.ToString();
    }

    public static string BuildReadSql(string database, IEnumerable<string> fields)
        => $"SELECT {string.Join(", ", fields)} FROM {database}.{TABLE_NAME} WHERE {PRIMARY_KEY} = ?";

    // Placeholders: [value_1, ..., value_N, key].
    public static string BuildUpdateSql(string database, IEnumerable<string> fields)
        => $"UPDATE {database}.{TABLE_NAME} SET {string.Join(", ", fields.Select(f => $"{f} = ?"))} WHERE {PRIMARY_KEY} = ?";

    // Result columns: [primaryKey, field_1, ..., field_N]. Placeholders: [startKey, count].
    public static string BuildScanSql(string database, IEnumerable<string> fields)
        => $"SELECT {PRIMARY_KEY}, {string.Join(", ", fields)} FROM {database}.{TABLE_NAME} " +
           $"WHERE {PRIMARY_KEY} >= ? ORDER BY {PRIMARY_KEY} LIMIT ?";
}
