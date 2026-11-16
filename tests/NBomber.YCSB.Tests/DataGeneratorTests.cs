using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Tests
{
    public class DataGeneratorTests
    {
        private const int Draws = 10_000;

        private static void AssertValues(Dictionary<string, string> values, int fieldCount, int fieldLength)
        {
            var expected = Enumerable.Range(1, fieldCount).Select(i => $"field{i}").ToHashSet();
            Assert.True(expected.SetEquals(values.Keys));

            foreach (var value in values.Values)
            {
                Assert.Equal(fieldLength, value.Length);
                Assert.All(value, c => Assert.InRange(c, 'a', 'z'));
            }
        }

        [Fact]
        public void CreateValues_Has_Configured_Length_And_Lowercase_Alphabet()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 100 });

            AssertValues(dataGen.CreateValues(), 10, 100);
        }

        [Fact]
        public void CreateValues_With_FieldLength_One_Returns_Single_Letter()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 1 });

            AssertValues(dataGen.CreateValues(), 10, 1);
        }

        [Fact]
        public void CreateValuesToUpdate_WriteAllFields_Returns_All_Fields()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 20, WriteAllFields = true });

            AssertValues(dataGen.CreateValuesToUpdate(), 10, 20);
        }

        [Fact]
        public void CreateValuesToUpdate_Single_Field_Returns_One_Field()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 20, WriteAllFields = false });

            var values = dataGen.CreateValuesToUpdate();

            Assert.Single(values);
            Assert.All(values.Values, v => Assert.Equal(20, v.Length));
        }

        [Fact]
        public void Single_Field_Choice_Covers_field1_To_fieldN_Only()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs
            {
                FieldCount = 10, FieldLength = 5, ReadAllFields = false, WriteAllFields = false
            });
            var expected = Enumerable.Range(1, 10).Select(i => $"field{i}").ToHashSet();
            var seenRead = new HashSet<string>();
            var seenWrite = new HashSet<string>();

            for (var i = 0; i < Draws; i++)
            {
                var read = Assert.Single(dataGen.GetFieldNames()!);
                var write = Assert.Single(dataGen.CreateValuesToUpdate()).Key;

                Assert.Contains(read, expected);
                Assert.Contains(write, expected);
                seenRead.Add(read);
                seenWrite.Add(write);
            }

            Assert.Contains("field1", seenRead);
            Assert.Contains("field10", seenRead);
            Assert.Contains("field1", seenWrite);
            Assert.Contains("field10", seenWrite);
        }

        [Fact]
        public void Single_Field_Choice_With_FieldCount_One_Is_field1()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs
            {
                FieldCount = 1, FieldLength = 5, ReadAllFields = false, WriteAllFields = false
            });

            for (var i = 0; i < 100; i++)
            {
                Assert.Equal("field1", Assert.Single(dataGen.GetFieldNames()!));
                Assert.Equal("field1", Assert.Single(dataGen.CreateValuesToUpdate()).Key);
            }
        }

        [Fact]
        public void GetFieldNames_Returns_Null_When_ReadAllFields()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { ReadAllFields = true });

            Assert.Null(dataGen.GetFieldNames());
        }

        [Fact]
        public void Generation_Is_Safe_Under_Parallel_Use()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs
            {
                FieldCount = 10, FieldLength = 100, ReadAllFields = false
            });
            var expected = Enumerable.Range(1, 10).Select(i => $"field{i}").ToHashSet();

            Parallel.For(0, Draws, _ =>
            {
                AssertValues(dataGen.CreateValues(), 10, 100);

                var name = Assert.Single(dataGen.GetFieldNames()!);
                Assert.Contains(name, expected);
            });
        }

        [Fact]
        public void GetKeyNext_Is_Unique_Under_Parallel_Use()
        {
            const int count = 10_000;
            var dataGen = new DataGenerator(new YcsbCliArgs { InsertOrder = "ordered", RecordCount = 1000 });
            var keys = new string[count];

            Parallel.For(0, count, i => keys[i] = dataGen.GetKeyNext());

            var expected = Enumerable.Range(1001, count).Select(n => $"user{n}").ToHashSet();
            Assert.Equal(count, keys.Distinct().Count());
            Assert.True(expected.SetEquals(keys));
        }
    }
}
