namespace NBomber.YCSB.Infra;

/// <summary>
/// Represents the standard YCSB workloads used to model different read/write access patterns.
/// </summary>
public enum Workload
{
    /// <summary>
    /// Update-heavy workload (50% inserts, 50% reads).
    /// </summary>
    A,

    /// <summary>
    /// Read-heavy workload (95% reads, 5% updates).
    /// </summary>
    B,

    /// <summary>
    /// Read-only workload (100% reads).
    /// </summary>
    C,

    ///// <summary>
    ///// Read-latest workload, typically targeting recently inserted keys.
    ///// </summary>
    //D,

    /// <summary>
    /// Short-range workload performing scans with occasional inserts.
    /// </summary>
    E
}

/// <summary>
/// Defines the types of database operations used in YCSB workloads.
/// </summary>
public enum OperationType
{
    /// <summary>
    /// Represents an insert operation.
    /// </summary>
    Insert,

    /// <summary>
    /// Represents a read operation.
    /// </summary>
    Read,

    /// <summary>
    /// Represents an update operation.
    /// </summary>
    Update,

    /// <summary>
    /// Represents a scan operation, typically used for range queries.
    /// </summary>
    Scan
}

/// <summary>
/// Provides helper methods for retrieving operation distributions
/// and descriptions associated with YCSB workloads.
/// </summary>
public static class WorkloadManager
{
    /// <summary>
    /// Returns the operation mix for the specified YCSB workload.
    /// </summary>
    /// <param name="workload">The workload type.</param>
    /// <returns>
    /// An array of tuples, where each tuple contains:
    /// <list type="bullet">
    /// <item>
    /// <description><see cref="OperationType"/> — the operation type.</description>
    /// </item>
    /// <item>
    /// <description><c>int</c> — the relative weight or percentage of that operation in the workload.</description>
    /// </item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an unsupported workload value is provided.
    /// </exception>
    public static (OperationType, int)[] GetOperations(Workload workload)
    {
        return workload switch
        {
            Workload.A => [(OperationType.Insert, 50), (OperationType.Read, 50)],
            Workload.B => [(OperationType.Read, 95), (OperationType.Update, 5)],
            Workload.C => [(OperationType.Read, 100)],
            //Workload.D => [(OperationType.ReadLatest, 95), (OperationType.Insert, 5)],
            Workload.E => [(OperationType.Scan, 95), (OperationType.Insert, 5)],
            _ => throw new ArgumentOutOfRangeException(nameof(workload), workload, null)
        };
    }

    /// <summary>
    /// Returns a human-readable description for the given workload.
    /// </summary>
    /// <param name="workload">The workload to describe.</param>
    /// <returns>A descriptive string representing the workload category.</returns>
    public static string GetDescription(Workload workload)
    {
        return workload switch
        {
            Workload.A => "Update Heavy",
            Workload.B => "Read Heavy",
            Workload.C => "Read Only",
            //Workload.D => "Read Latest",
            Workload.E => "Short Range",
            _ => ""
        };
    }
}
