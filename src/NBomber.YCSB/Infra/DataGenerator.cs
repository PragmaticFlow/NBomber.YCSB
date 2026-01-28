using Bogus;
using NBomber.Contracts;
using NBomber.CSharp;
using Standart.Hash.xxHash;

namespace NBomber.YCSB.Infra;

public class DataGenerator(YcsbCliArgs settings)
{
    private readonly int _zeroPadding = settings.ZeroPadding;
    private readonly int _fieldCount = settings.FieldCount;
    private readonly int _fieldLength = settings.FieldLength;
    private readonly bool _orderedInserts = settings.InsertOrder.Equals("ordered", StringComparison.OrdinalIgnoreCase);
    private readonly ThreadLocal<Faker> _faker = new(() => new Faker());
    
    private ulong _recordCount = settings.RecordCount;

    public void SetRecordCount(ulong insertedCount)
    {
        _recordCount = insertedCount;
    }  

    public string GetKeyNext()
    {
        var keyNum = Interlocked.Increment(ref _recordCount);
        return BuildKeyName(keyNum, _zeroPadding);
    }

    public string GetKeyZipf(IScenarioContext context)
    {
        var keyNum = context.Random.Zipf((int)_recordCount, 1.3);
        return BuildKeyName((ulong)keyNum, _zeroPadding);
    }

    public string GetKeyLatest(IScenarioContext context, double theta = 0.99)
    {
        var max = Interlocked.Read(ref _recordCount);

        if (max <= 1UL)
            return BuildKeyName(1UL, _zeroPadding);

        var min = Math.Min(max, int.MaxValue);

        var rundom = context.Random.Zipf((int)min, theta);

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
        return _faker.Value!.Random.String2(_fieldLength, "abcdefghijklmnopqrstuvwxyz");
    }

    private string[] GenerateFields()
    {
        return Enumerable.Range(1, _fieldCount)
            .Select(i => $"field{i}")
            .ToArray();
    }
}

