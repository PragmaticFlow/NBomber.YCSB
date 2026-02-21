using NBomber.Contracts;

namespace NBomber.YCSB.DAL;

/// <summary>
/// Defines the contract for a YCSB-compatible database client.
/// Provides the core CRUD and scan operations used by YCSB workloads.
/// </summary>
public interface IDbYcsbClient
{
    /// <summary>
    /// Inserts a new record into the specified table.
    /// </summary>
    /// <param name="table">The logical table name.</param>
    /// <param name="key">The primary key of the record.</param>
    /// <param name="values">A dictionary of field/value pairs for the record.</param>
    /// <returns>
    /// A <see cref="Response{object}"/> indicating success or failure of the operation.
    /// </returns>
    Task<Response<object>> Insert(string table, string key, Dictionary<string, string> values);

    /// <summary>
    /// Updates an existing record in the specified table.
    /// </summary>
    /// <param name="table">The logical table name.</param>
    /// <param name="key">The primary key of the record to update.</param>
    /// <param name="values">The field/value pairs to update.</param>
    /// <returns>
    /// A <see cref="Response{object}"/> describing the result of the update operation.
    /// </returns>
    Task<Response<object>> Update(string table, string key, Dictionary<string, string> values);

    /// <summary>
    /// Reads a record from the specified table by key.
    /// </summary>
    /// <param name="table">The logical table name.</param>
    /// <param name="key">The primary key of the record to read.</param>
    /// <param name="fields">
    /// The set of fields to retrieve.
    /// If null or empty, all fields should be returned.
    /// </param>
    /// <returns>
    /// A <see cref="Response{object}"/> containing the retrieved record, if found.
    /// </returns>
    Task<Response<object>> Read(string table, string key, HashSet<string>? fields);

    /// <summary>
    /// Performs a read-modify-write operation (YCSB workload F) on a record in the specified table.
    /// Reads the record first, then applies the given field updates and writes it back.
    /// The reported size includes bytes from both the read and the write phases.
    /// </summary>
    /// <param name="table">The logical table name.</param>
    /// <param name="key">The primary key of the record to read and update.</param>
    /// <param name="fields">
    /// The set of fields to retrieve during the read phase.
    /// If null or empty, all fields are returned.
    /// </param>
    /// <param name="values">The field/value pairs to apply during the write phase.</param>
    /// <returns>
    /// A <see cref="Response{object}"/> with the total size of the read and write payloads,
    /// or a failure response if the record does not exist.
    /// </returns>
    Task<Response<object>> ReadModifyWrite(string table, string key, HashSet<string>? fields, Dictionary<string, string> values);

    /// <summary>
    /// Performs a scan operation starting at <paramref name="startKey"/> and reading sequential records.
    /// </summary>
    /// <param name="table">The logical table name.</param>
    /// <param name="startKey">The primary key to start scanning from.</param>
    /// <param name="count">The maximum number of records to scan.</param>
    /// <param name="fields">
    /// The set of fields to retrieve for each record.
    /// If null or empty, all fields should be returned.
    /// </param>
    /// <returns>
    /// A <see cref="Response{object}"/> containing the scanned records.
    /// </returns>
    Task<Response<object>> Scan(string table, string startKey, int count, HashSet<string>? fields);

    /// <summary>
    /// Deletes all data from the underlying database or storage system.
    /// Intended for test setup or teardown operations.
    /// </summary>
    /// <returns>
    /// A <see cref="Response{object}"/> indicating the result of the delete-all operation.
    /// </returns>
    Task<Response<object>> DeleteAllData();

    /// <summary>
    /// Performs a bulk insert operation for efficiently loading initial data.
    /// </summary>
    /// <param name="table">The logical table name.</param>
    /// <param name="data">
    /// A dictionary where each key is a record key, and the value is a dictionary of field/value pairs.
    /// </param>
    /// <returns>
    /// A <see cref="Response{object}"/> representing the result of the bulk insert.
    /// </returns>
    Task<Response<object>> BulkInsert(string table, Dictionary<string, Dictionary<string, string>> data);

    /// <summary>
    /// Initializes the database or storage layer before running workloads.
    /// Typically used for creating tables, indexes, or establishing connections.
    /// </summary>
    /// <returns>
    /// A <see cref="Response{T}"/> describing the initialization result.
    /// </returns>
    Task<Response<object>> InitDb();
}
