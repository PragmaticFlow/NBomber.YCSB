using NBomber.YCSB.KeyDb;
using StackExchange.Redis;

namespace NBomber.YCSB.Tests
{
    public class KeyDbHelperTests
    {
        [Fact]
        public void GetSize_String_Returns_Utf8_Byte_Count()
        {
            Assert.Equal(3, KeyDbHelper.GetSize("abc"));
            Assert.Equal(4, KeyDbHelper.GetSize("abé"));
        }

        [Fact]
        public void GetSize_Null_String_Returns_Zero()
        {
            Assert.Equal(0, KeyDbHelper.GetSize((string?)null));
        }

        [Fact]
        public void GetSize_RedisValues_With_Missing_Field_Does_Not_Throw()
        {
            RedisValue[] values = ["abc", RedisValue.Null, "de"];

            Assert.Equal(5, KeyDbHelper.GetSize(values));
        }

        [Fact]
        public void GetSize_Nested_RedisValues_With_Missing_Field_Does_Not_Throw()
        {
            RedisValue[][] values = [["abc", RedisValue.Null], ["de"]];

            Assert.Equal(5, KeyDbHelper.GetSize(values));
        }

        [Fact]
        public void GetSize_HashEntries_Sums_Field_Names_And_Values()
        {
            HashEntry[] entries = [new HashEntry("field0", "value0"), new HashEntry("f1", "v1")];

            Assert.Equal(16, KeyDbHelper.GetSize(entries));
        }

        [Fact]
        public void AllNull_Is_True_When_Every_Value_Is_Null()
        {
            Assert.True(KeyDbHelper.AllNull([RedisValue.Null, RedisValue.Null]));
        }

        [Fact]
        public void AllNull_Is_False_When_Any_Value_Exists()
        {
            Assert.False(KeyDbHelper.AllNull([RedisValue.Null, "x"]));
        }
    }
}
