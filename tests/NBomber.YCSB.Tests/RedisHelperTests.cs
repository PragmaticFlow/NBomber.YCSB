using NBomber.YCSB.Redis;
using StackExchange.Redis;

namespace NBomber.YCSB.Tests
{
    public class RedisHelperTests
    {
        [Fact]
        public void GetSize_String_Returns_Utf8_Byte_Count()
        {
            Assert.Equal(3, RedisHelper.GetSize("abc"));
            Assert.Equal(4, RedisHelper.GetSize("abé"));
        }

        [Fact]
        public void GetSize_Null_String_Returns_Zero()
        {
            Assert.Equal(0, RedisHelper.GetSize((string?)null));
        }

        [Fact]
        public void GetSize_RedisValues_With_Missing_Field_Does_Not_Throw()
        {
            RedisValue[] values = ["abc", RedisValue.Null, "de"];

            Assert.Equal(5, RedisHelper.GetSize(values));
        }

        [Fact]
        public void GetSize_Nested_RedisValues_With_Missing_Field_Does_Not_Throw()
        {
            RedisValue[][] values = [["abc", RedisValue.Null], ["de"]];

            Assert.Equal(5, RedisHelper.GetSize(values));
        }

        [Fact]
        public void GetSize_HashEntries_Sums_Field_Names_And_Values()
        {
            HashEntry[] entries = [new HashEntry("field0", "value0"), new HashEntry("f1", "v1")];

            Assert.Equal(16, RedisHelper.GetSize(entries));
        }

        [Fact]
        public void AllNull_Is_True_When_Every_Value_Is_Null()
        {
            Assert.True(RedisHelper.AllNull([RedisValue.Null, RedisValue.Null]));
        }

        [Fact]
        public void AllNull_Is_False_When_Any_Value_Exists()
        {
            Assert.False(RedisHelper.AllNull([RedisValue.Null, "x"]));
        }
    }
}
