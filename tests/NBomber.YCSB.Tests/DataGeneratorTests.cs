using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Tests
{
    public class DataGeneratorTests
    {
        private const int LATEST_SEED = 12345;
        private const int ZIPF_SEED = 6789;
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

            Parallel.For(0, count, i => keys[i] = dataGen.GetKeyNext().Key);

            var expected = Enumerable.Range(1001, count).Select(n => $"user{n}").ToHashSet();
            Assert.Equal(count, keys.Distinct().Count());
            Assert.True(expected.SetEquals(keys));
        }

        private static DataGenerator CreateOrdered(ulong recordCount = 1000) =>
            new(new YcsbCliArgs { InsertOrder = "ordered", RecordCount = recordCount });

        private static ulong KeyNumber(string key) => ulong.Parse(key["user".Length..]);

        [Fact]
        public void LastAcknowledgedKey_Starts_At_RecordCount()
        {
            Assert.Equal(1000UL, CreateOrdered().LastAcknowledgedKey);
        }

        [Fact]
        public void AcknowledgeInsert_Out_Of_Order_Advances_Only_Contiguously()
        {
            var dataGen = CreateOrdered();
            Assert.Equal(1001UL, dataGen.GetKeyNext().KeyNum);
            Assert.Equal(1002UL, dataGen.GetKeyNext().KeyNum);
            Assert.Equal(1003UL, dataGen.GetKeyNext().KeyNum);

            dataGen.AcknowledgeInsert(1003);
            Assert.Equal(1000UL, dataGen.LastAcknowledgedKey);

            dataGen.AcknowledgeInsert(1001);
            Assert.Equal(1001UL, dataGen.LastAcknowledgedKey);

            dataGen.AcknowledgeInsert(1002);
            Assert.Equal(1003UL, dataGen.LastAcknowledgedKey);
        }

        [Fact]
        public void AcknowledgeInsert_Duplicate_Does_Not_Change_Limit()
        {
            var dataGen = CreateOrdered();
            dataGen.GetKeyNext();

            dataGen.AcknowledgeInsert(1001);
            dataGen.AcknowledgeInsert(1001);

            Assert.Equal(1001UL, dataGen.LastAcknowledgedKey);
        }

        [Fact]
        public void GetKeyLatest_Never_Returns_Unacknowledged_Key()
        {
            var dataGen = CreateOrdered();
            var random = new Random(LATEST_SEED);
            dataGen.GetKeyNext();

            var seen = new HashSet<ulong>();
            for (var i = 0; i < Draws; i++)
                seen.Add(KeyNumber(dataGen.GetKeyLatest(random)));

            Assert.Contains(1000UL, seen);
            Assert.True(seen.Max() <= 1000UL);

            dataGen.AcknowledgeInsert(1001);
            seen.Clear();
            for (var i = 0; i < Draws; i++)
                seen.Add(KeyNumber(dataGen.GetKeyLatest(random)));

            Assert.Contains(1001UL, seen);
            Assert.True(seen.Max() <= 1001UL);
        }

        [Fact]
        public void GetKeyZipf_Never_Returns_Unacknowledged_Key()
        {
            var dataGen = CreateOrdered(recordCount: 2);
            var random = new Random(ZIPF_SEED);
            dataGen.GetKeyNext();

            var seen = new HashSet<ulong>();
            for (var i = 0; i < Draws; i++)
                seen.Add(KeyNumber(dataGen.GetKeyZipf(random)));

            Assert.True(seen.SetEquals([1UL, 2UL]));

            dataGen.AcknowledgeInsert(3);
            for (var i = 0; i < Draws; i++)
                seen.Add(KeyNumber(dataGen.GetKeyZipf(random)));

            Assert.Contains(3UL, seen);
        }

        [Fact]
        public void Reserve_And_Acknowledge_Are_Safe_Under_Parallel_Use()
        {
            const int count = 10_000;
            var dataGen = CreateOrdered();
            var keyNums = new ulong[count];

            Parallel.For(0, count, i =>
            {
                var (keyNum, _) = dataGen.GetKeyNext();
                keyNums[i] = keyNum;
                dataGen.AcknowledgeInsert(keyNum);
            });

            Assert.True(Enumerable.Range(1001, count).Select(n => (ulong)n).ToHashSet().SetEquals(keyNums));
            Assert.Equal(count, keyNums.Distinct().Count());
            Assert.Equal(11000UL, dataGen.LastAcknowledgedKey);
        }
    }
}
