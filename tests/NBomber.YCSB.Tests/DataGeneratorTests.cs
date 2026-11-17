using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Tests
{
    public class DataGeneratorTests
    {
        private const int LATEST_SEED = 12345;
        private const int ZIPF_SEED = 6789;
        private const int DRAWS = 10_000;

        private static DataGenerator CreateOrdered(ulong recordCount = 1000) =>
            new(new YcsbCliArgs { InsertOrder = "ordered", RecordCount = recordCount }, insertProportion: 0);

        private static ulong KeyNumber(string key) => ulong.Parse(key["user".Length..]);

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

        private static Dictionary<ulong, int> CountLatest(DataGenerator dataGen, int seed, int draws)
        {
            var random = new Random(seed);
            var counts = new Dictionary<ulong, int>();
            for (var i = 0; i < draws; i++)
            {
                var key = KeyNumber(dataGen.GetKeyLatest(random));
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }

            return counts;
        }

        [Fact]
        public void CreateValues_Has_Configured_Length_And_Lowercase_Alphabet()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 100 }, insertProportion: 0);

            AssertValues(dataGen.CreateValues(), 10, 100);
        }

        [Fact]
        public void CreateValues_With_FieldLength_One_Returns_Single_Letter()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 1 }, insertProportion: 0);

            AssertValues(dataGen.CreateValues(), 10, 1);
        }

        [Fact]
        public void CreateValuesToUpdate_WriteAllFields_Returns_All_Fields()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 20, WriteAllFields = true }, insertProportion: 0);

            AssertValues(dataGen.CreateValuesToUpdate(), 10, 20);
        }

        [Fact]
        public void CreateValuesToUpdate_Single_Field_Returns_One_Field()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { FieldCount = 10, FieldLength = 20, WriteAllFields = false }, insertProportion: 0);

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
            }, insertProportion: 0);
            var expected = Enumerable.Range(1, 10).Select(i => $"field{i}").ToHashSet();
            var seenRead = new HashSet<string>();
            var seenWrite = new HashSet<string>();

            for (var i = 0; i < DRAWS; i++)
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
            }, insertProportion: 0);

            for (var i = 0; i < 100; i++)
            {
                Assert.Equal("field1", Assert.Single(dataGen.GetFieldNames()!));
                Assert.Equal("field1", Assert.Single(dataGen.CreateValuesToUpdate()).Key);
            }
        }

        [Fact]
        public void GetFieldNames_Returns_Null_When_ReadAllFields()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs { ReadAllFields = true }, insertProportion: 0);

            Assert.Null(dataGen.GetFieldNames());
        }

        [Fact]
        public void Generation_Is_Safe_Under_Parallel_Use()
        {
            var dataGen = new DataGenerator(new YcsbCliArgs
            {
                FieldCount = 10, FieldLength = 100, ReadAllFields = false
            }, insertProportion: 0);
            var expected = Enumerable.Range(1, 10).Select(i => $"field{i}").ToHashSet();

            Parallel.For(0, DRAWS, _ =>
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
            var dataGen = new DataGenerator(new YcsbCliArgs { InsertOrder = "ordered", RecordCount = 1000 }, insertProportion: 0);
            var keys = new string[count];

            Parallel.For(0, count, i => keys[i] = dataGen.GetKeyNext().Key);

            var expected = Enumerable.Range(1001, count).Select(n => $"user{n}").ToHashSet();
            Assert.Equal(count, keys.Distinct().Count());
            Assert.True(expected.SetEquals(keys));
        }

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
            for (var i = 0; i < DRAWS; i++)
                seen.Add(KeyNumber(dataGen.GetKeyLatest(random)));

            Assert.Contains(1000UL, seen);
            Assert.True(seen.Max() <= 1000UL);

            dataGen.AcknowledgeInsert(1001);
            seen.Clear();
            for (var i = 0; i < DRAWS; i++)
                seen.Add(KeyNumber(dataGen.GetKeyLatest(random)));

            Assert.Contains(1001UL, seen);
            Assert.True(seen.Max() <= 1001UL);
        }

        [Fact]
        public void GetKeyLatest_Newest_Key_Is_Hottest_With_Zipfian_Shape()
        {
            const int draws = 50_000;
            var counts = CountLatest(CreateOrdered(), LATEST_SEED, draws);

            Assert.InRange(counts.Keys.Min(), 1UL, 1000UL);
            Assert.InRange(counts.Keys.Max(), 1UL, 1000UL);
            Assert.Equal(1000UL, counts.MaxBy(kv => kv.Value).Key);
            Assert.InRange(counts[1000] / (double)draws, 0.11, 0.15);
            Assert.InRange(counts[999] / (double)draws, 0.055, 0.075);
        }

        [Fact]
        public void GetKeyLatest_Newly_Acknowledged_Key_Becomes_Hottest()
        {
            var dataGen = CreateOrdered();
            dataGen.AcknowledgeInsert(dataGen.GetKeyNext().KeyNum);

            var counts = CountLatest(dataGen, LATEST_SEED, 50_000);

            Assert.Equal(1001UL, counts.MaxBy(kv => kv.Value).Key);
            Assert.True(counts.Keys.Max() <= 1001UL);
        }

        [Fact]
        public void GetKeyLatest_Incremental_Growth_Equals_Full_Dataset()
        {
            var grown = CreateOrdered(1000);
            for (var i = 0; i < 1000; i++)
                grown.AcknowledgeInsert(grown.GetKeyNext().KeyNum);
            var full = CreateOrdered(2000);

            var randomGrown = new Random(LATEST_SEED);
            var randomFull = new Random(LATEST_SEED);
            for (var i = 0; i < 1000; i++)
                Assert.Equal(full.GetKeyLatest(randomFull), grown.GetKeyLatest(randomGrown));
        }

        [Fact]
        public void GetKeyLatest_Single_Record_Always_Returns_Key_One()
        {
            var dataGen = CreateOrdered(1);
            Assert.Equal(new[] { 1UL }, CountLatest(dataGen, LATEST_SEED, 1000).Keys);

            dataGen.GetKeyNext();   // key 2 reserved, not acknowledged
            Assert.Equal(new[] { 1UL }, CountLatest(dataGen, LATEST_SEED, 1000).Keys);
        }

        [Fact]
        public void GetKeyLatest_Two_Records_Return_Only_Keys_One_And_Two()
        {
            const int draws = 20_000;
            var counts = CountLatest(CreateOrdered(2), LATEST_SEED, draws);

            Assert.True(counts.Keys.ToHashSet().IsSubsetOf([1UL, 2UL]));
            Assert.InRange(counts[2] / (double)draws, 0.60, 0.73);
        }

        [Fact]
        public void Constructor_Rejects_RecordCount_Zero_For_Workload_D()
        {
            var ex = Assert.Throws<ArgumentException>(() => CreateOrdered(0));
            Assert.Contains("recordcount", ex.Message);
        }

        [Fact]
        public void GetKeyZipf_Never_Returns_Unacknowledged_Key()
        {
            var dataGen = CreateOrdered(recordCount: 2);
            var random = new Random(ZIPF_SEED);
            dataGen.GetKeyNext();

            var seen = new HashSet<ulong>();
            for (var i = 0; i < DRAWS; i++)
                seen.Add(KeyNumber(dataGen.GetKeyZipf(random)));

            Assert.True(seen.SetEquals([1UL, 2UL]));

            dataGen.AcknowledgeInsert(3);
            for (var i = 0; i < DRAWS; i++)
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
