using NBomber.YCSB.CockroachDb;
using NBomber.YCSB.Infra;
using Npgsql;

namespace NBomber.YCSB.Tests
{
    public class CockroachDbHelperTests
    {
        [Fact]
        public void BuildConnectionString_Uses_Cockroach_Defaults()
        {
            var builder = CockroachDbHelper.BuildConnectionString(new Dictionary<string, string>());

            Assert.Equal("localhost", builder.Host);
            Assert.Equal(26257, builder.Port);
            Assert.Equal("defaultdb", builder.Database);
            Assert.Equal("root", builder.Username);
            Assert.True(string.IsNullOrEmpty(builder.Password));
        }

        [Fact]
        public void BuildConnectionString_Reads_Connection_Props()
        {
            var props = YcsbCliArgs.ParseProps([
                "cockroachdb.host=dbhost",
                "cockroachdb.port=26258",
                "cockroachdb.database=ycsb",
                "cockroachdb.user=bench",
                "cockroachdb.password=secret"
            ]);

            var builder = CockroachDbHelper.BuildConnectionString(props);

            Assert.Equal("dbhost", builder.Host);
            Assert.Equal(26258, builder.Port);
            Assert.Equal("ycsb", builder.Database);
            Assert.Equal("bench", builder.Username);
            Assert.Equal("secret", builder.Password);
        }

        [Fact]
        public void BuildConnectionString_Ignores_Postgres_Props()
        {
            var props = YcsbCliArgs.ParseProps(["postgres.host=pghost", "postgres.port=5432"]);

            var builder = CockroachDbHelper.BuildConnectionString(props);

            Assert.Equal("localhost", builder.Host);
            Assert.Equal(26257, builder.Port);
        }

        [Fact]
        public void BuildConnectionString_Enables_Auto_Prepare_And_Keeps_Pool_Settings()
        {
            var builder = CockroachDbHelper.BuildConnectionString(new Dictionary<string, string>());

            Assert.Equal(20, builder.MaxAutoPrepare);
            Assert.Equal(5, builder.AutoPrepareMinUsages);
            Assert.Equal(1, builder.MinPoolSize);
            Assert.Equal(100, builder.MaxPoolSize);
        }

        [Fact]
        public void BuildProjection_Gives_Same_Sql_For_Different_Fields()
        {
            var first = CockroachDbHelper.BuildProjection(["field1"], new NpgsqlCommand().Parameters);
            var second = CockroachDbHelper.BuildProjection(["field7"], new NpgsqlCommand().Parameters);

            Assert.Equal(first, second);
            Assert.DoesNotContain("field", first);
        }

        [Fact]
        public void BuildProjection_Adds_Field_Names_As_Parameters()
        {
            var parameters = new NpgsqlCommand().Parameters;

            CockroachDbHelper.BuildProjection(["field1", "field7"], parameters);

            Assert.Equal(2, parameters.Count);
            Assert.Equal("field1", parameters["f0"].Value);
            Assert.Equal("field7", parameters["f1"].Value);
        }

        [Fact]
        public void GetSize_Sums_Key_And_Json_Lengths()
        {
            Assert.Equal(16, CockroachDbHelper.GetSize("user1", "{\"a\":\"bcd\"}"));
        }

        [Fact]
        public void GetSize_Counts_Utf8_Bytes()
        {
            Assert.Equal(7, CockroachDbHelper.GetSize("key", "éé"));
        }

        [Fact]
        public void GetSize_Returns_Key_Length_For_Empty_Json()
        {
            Assert.Equal(5, CockroachDbHelper.GetSize("user1", ""));
        }
    }
}
