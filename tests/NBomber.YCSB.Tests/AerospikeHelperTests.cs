using Aerospike.Client;
using NBomber.YCSB.AerospikeDb;

namespace NBomber.YCSB.Tests
{
    public class AerospikeHelperTests
    {
        [Fact]
        public void GetSize_Counts_Key_Names_And_Values_In_Utf8_Bytes()
        {
            var size = AerospikeHelper.GetSize("k", new Dictionary<string, string> { ["f1"] = "abc", ["f2"] = "é" });

            // 1 + (2 + 3) + (2 + 2)
            Assert.Equal(10, size);
        }

        [Fact]
        public void GetSize_Of_Key_Only_Counts_Key()
            => Assert.Equal(3, AerospikeHelper.GetSize("key", (IDictionary<string, string>?)null));

        [Fact]
        public void GetSize_Of_Record_Matches_Size_Of_Equivalent_Dictionary()
        {
            var values = new Dictionary<string, string> { ["field1"] = "hello", ["field2"] = "wörld" };
            var record = new Aerospike.Client.Record(values.ToDictionary(kv => kv.Key, kv => (object)kv.Value), 1, 0);

            Assert.Equal(AerospikeHelper.GetSize("k", values), AerospikeHelper.GetSize("k", record));
        }

        [Fact]
        public void BuildBins_Maps_Each_Field_To_A_Bin_With_Its_Value()
        {
            var values = new Dictionary<string, string> { ["field1"] = "v1", ["field2"] = "v2", ["field3"] = "v3" };

            var bins = AerospikeHelper.BuildBins(values);

            Assert.Equal(3, bins.Length);
            Assert.Equal(values, bins.ToDictionary(b => b.name, b => (string)b.value.Object));
        }

        [Fact]
        public void BuildBins_Writes_Null_Value_As_Empty_String()
        {
            var bins = AerospikeHelper.BuildBins(new Dictionary<string, string> { ["field1"] = null! });

            Assert.Equal("", (string)bins[0].value.Object);
        }

        [Fact]
        public void HasNamespace_Matches_Exact_Names_In_Info_Reply()
        {
            Assert.True(AerospikeHelper.HasNamespace("test;bar\n", "bar"));
            Assert.True(AerospikeHelper.HasNamespace("test", "test"));
            Assert.False(AerospikeHelper.HasNamespace("test;bar", "tes"));
            Assert.False(AerospikeHelper.HasNamespace("", "test"));
        }

        [Fact]
        public void GetMaxConcurrency_Defaults_To_512_When_Absent()
            => Assert.Equal(512, AerospikeHelper.GetMaxConcurrency(new Dictionary<string, string>()));

        [Fact]
        public void GetMaxConcurrency_Returns_Custom_Value()
            => Assert.Equal(64, AerospikeHelper.GetMaxConcurrency(new Dictionary<string, string> { ["aerospike.maxconcurrency"] = "64" }));

        [Theory]
        [InlineData("0")]
        [InlineData("-5")]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("99999999999")]
        public void GetMaxConcurrency_Rejects_Invalid_Value(string value)
        {
            var props = new Dictionary<string, string> { ["aerospike.maxconcurrency"] = value };

            var ex = Assert.Throws<ArgumentException>(() => AerospikeHelper.GetMaxConcurrency(props));

            Assert.Contains("aerospike.maxconcurrency", ex.Message);
            Assert.Contains(value, ex.Message);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("abc")]
        public void Constructor_Rejects_Invalid_MaxConcurrency_Before_Connecting(string value)
        {
            var props = new Dictionary<string, string> { ["aerospike.maxconcurrency"] = value };

            var ex = Assert.Throws<ArgumentException>(() => new AerospikeYcsbClient(props));

            Assert.Contains("aerospike.maxconcurrency", ex.Message);
        }
    }
}
