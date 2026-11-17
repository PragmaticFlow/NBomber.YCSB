using NBomber.CSharp;
using Standart.Hash.xxHash;

namespace NBomber.YCSB.Infra;

public class DataGenerator(YcsbCliArgs settings)
{
    private const string ALPHABET = "abcdefghijklmnopqrstuvwxyz";

    private readonly int _zeroPadding = settings.ZeroPadding;
    private readonly int _fieldCount = settings.FieldCount;
    private readonly int _fieldLength = settings.FieldLength;
    private readonly bool _orderedInserts = settings.InsertOrder.Equals("ordered", StringComparison.OrdinalIgnoreCase);
    private readonly bool _readAllFields = settings.ReadAllFields;
    private readonly bool _writeAllFields = settings.WriteAllFields;
    private readonly int _minScanLength = settings.MinScanLength;
    private readonly int _maxScanLength = settings.GetMaxScanLength();

    private readonly ulong _recordCount = settings.RecordCount;   // initial load size, used by GenerateRandoms
    private readonly object _ackLock = new();
    private readonly HashSet<ulong> _pendingAcks = [];

    private ulong _lastInsertKey = settings.RecordCount;          // last reserved insert key
    private ulong _lastAcknowledgedKey = settings.RecordCount;    // highest contiguous acknowledged key

    private readonly long _zipfianItemCount = GetZipfianItemCount(settings);

    public int NextScanLength(Random random) => (int)random.NextInt64(_minScanLength, (long)_maxScanLength + 1);
    public ulong LastAcknowledgedKey => Interlocked.Read(ref _lastAcknowledgedKey);
    public long ZipfianItemCount => _zipfianItemCount;

    public (ulong KeyNum, string Key) GetKeyNext()
    {
        var keyNum = Interlocked.Increment(ref _lastInsertKey);
        return (keyNum, BuildKeyName(keyNum, _zeroPadding));
    }

    public void AcknowledgeInsert(ulong keyNum)
    {
        lock (_ackLock)
        {
            _pendingAcks.Add(keyNum);

            var limit = _lastAcknowledgedKey;
            while (_pendingAcks.Remove(limit + 1))
                limit++;

            Interlocked.Exchange(ref _lastAcknowledgedKey, limit);
        }
    }

    public string GetKeyZipf(Random random)
    {
        // the constructor rejects recordcount 0 for workloads that call this, so LastAcknowledgedKey >= 1
        var keyNum = random.ScrambledZipf(_zipfianItemCount, LastAcknowledgedKey);
        return BuildKeyName(keyNum, _zeroPadding);
    }

    public string GetKeyLatest(Random random, double theta = 0.99)
    {
        var max = LastAcknowledgedKey;

        if (max <= 1UL)
            return BuildKeyName(1UL, _zeroPadding);

        var min = Math.Min(max, int.MaxValue);

        var rundom = random.Zipf((int)min, theta);

        var offset = (ulong)(rundom - 1);

        ulong keyNum = (max > offset) ? (max - offset) : 1UL;

        return BuildKeyName(keyNum, _zeroPadding);
    }

    public Dictionary<string, Dictionary<string, string>> GenerateRandoms()
    {
        var result = new Dictionary<string, Dictionary<string, string>>();

        var fields = GenerateFields();

        for (ulong i = 1; i <= _recordCount; i++)
        {
            var key = BuildKeyName(i, _zeroPadding);
            var fieldValues = new Dictionary<string, string>(fields.Length);

            foreach (var field in fields)
            {
                fieldValues[field] = GenerateFieldValue();
            }

            result[key] = fieldValues;
        }

        return result;
    }

    public Dictionary<string, string> CreateValues()
    {
        var fields = GenerateFields();
        var values = new Dictionary<string, string>(fields.Length);

        foreach (var field in fields)
            values[field] = GenerateFieldValue();

        return values;
    }

    public Dictionary<string, string> CreateValuesToUpdate()
    {
        if (_writeAllFields)
        {
            return CreateValues();
        }
        else
        {
            var randomFieldIndex = Random.Shared.Next(1, _fieldCount + 1);
            return new Dictionary<string, string>
            {
                [$"field{randomFieldIndex}"] = GenerateFieldValue()
            };
        }
    }

    public HashSet<string>? GetFieldNames()
    {
        if (!_readAllFields)
        {
            var randomFieldIndex = Random.Shared.Next(1, _fieldCount + 1);
            return new HashSet<string> { $"field{randomFieldIndex}" };
        }
        else
            return null;
    }

    private static long GetZipfianItemCount(YcsbCliArgs settings)
    {
        var operations = WorkloadManager.GetOperations(settings.Workload);
        var usesZipfian = operations.Any(o => o.Item1 is OperationType.Read or OperationType.Update
                                                      or OperationType.Scan or OperationType.ReadModifyWrite);
        if (usesZipfian && settings.RecordCount == 0)
            throw new ArgumentException(
                $"recordcount must be at least 1 for workload {settings.Workload}: read, update, scan and read-modify-write choose keys from loaded records.",
                nameof(settings));

        var insertProportion = (double)operations.Where(o => o.Item1 == OperationType.Insert).Sum(o => o.Item2)
                               / operations.Sum(o => o.Item2);
        var expectedNewKeys = Math.Max(0, (long)(settings.OperationCount * insertProportion * 2.0));

        // + 1: YCSB builds ScrambledZipfianGenerator(insertstart, insertstart + insertcount + expectednewkeys),
        // an inclusive range (CoreWorkload.java L519, ScrambledZipfianGenerator.java L87).
        try
        {
            return checked((long)settings.RecordCount + expectedNewKeys + 1);
        }
        catch (OverflowException)
        {
            throw new ArgumentException($"recordcount ({settings.RecordCount}) is too large for zipfian key choice.", nameof(settings));
        }
    }

    private string BuildKeyName(ulong keyNum, int zeroPadding)
    {
        ulong keyValue = keyNum;
        
        if (!_orderedInserts)
        {
            keyValue = ConvertToXxHash64(keyNum);
        }
        
        var value = keyValue.ToString();
        var fill = zeroPadding - value.Length;

        return "user" + new string('0', Math.Max(0, fill)) + value;
    }

    private ulong ConvertToXxHash64(ulong value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        return xxHash64.ComputeHash(bytes);
    }

    private string GenerateFieldValue()
    {
        return string.Create(_fieldLength, 0, static (chars, _) =>
        {
            for (var i = 0; i < chars.Length; i++)
                chars[i] = ALPHABET[Random.Shared.Next(ALPHABET.Length)];
        });
    }

    private string[] GenerateFields()
    {
        return Enumerable.Range(1, _fieldCount)
            .Select(i => $"field{i}")
            .ToArray();
    }
}

