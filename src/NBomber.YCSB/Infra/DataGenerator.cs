using Bogus;
using NBomber.Contracts;
using NBomber.CSharp;

namespace NBomber.YCSB.Infra;

public class DataGenerator(YcsbCliArgs settings)
{
    private readonly int _recordCount = settings.RecordCount;
    private readonly int _zeroPadding = settings.ZeroPadding;
    private readonly int _fieldCount = settings.FieldCount;
    private readonly int _fieldLength = settings.FieldLength;
    private readonly ThreadLocal<Faker> _faker = new(() => new Faker());

    public string GetKeyZipf(IScenarioContext context)
    {
        var keyNum = context.Random.Zipf(_recordCount, 1.3);
        return BuildKeyName(keyNum, _zeroPadding);
    }

    public string GetKeyUniform(IScenarioContext context)
    {
        var keyNum = context.Random.Next(1, _recordCount + 1);
        return BuildKeyName(keyNum, _zeroPadding);
    }

    public Dictionary<string, Dictionary<string, string>> GenerateRandoms()
    {
        var result = new Dictionary<string, Dictionary<string, string>>(_recordCount);

        var fields = GenerateFields();

        for (int i = 1; i <= _recordCount; i++)
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

    private string BuildKeyName(long keyNum, int zeroPadding)
    {
        var value = keyNum.ToString();
        var fill = zeroPadding - value.Length;

        return "user" + new string('0', Math.Max(0, fill)) + value;
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

