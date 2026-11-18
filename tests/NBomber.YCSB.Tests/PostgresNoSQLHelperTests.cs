using NBomber.YCSB.Infra;
using NBomber.YCSB.PosgresNoSQL;
using Npgsql;

namespace NBomber.YCSB.Tests
{
    public class PostgresNoSQLHelperTests
    {
        [Fact]
        public void BuildConnectionString_Enables_Auto_Prepare_And_Keeps_Pool_Settings()
        {
            var builder = PostgresNoSQLHelper.BuildConnectionString(new Dictionary<string, string>());

            Assert.True(builder.MaxAutoPrepare > 0);
            Assert.Equal(5, builder.AutoPrepareMinUsages);
            Assert.Equal(1, builder.MinPoolSize);
            Assert.Equal(100, builder.MaxPoolSize);
        }

        [Fact]
        public void BuildConnectionString_Reads_Connection_Props()
        {
            var props = YcsbCliArgs.ParseProps(["postgres.host=dbhost", "postgres.port=6543", "postgres.database=mydb"]);

            var builder = PostgresNoSQLHelper.BuildConnectionString(props);

            Assert.Equal("dbhost", builder.Host);
            Assert.Equal(6543, builder.Port);
            Assert.Equal("mydb", builder.Database);
        }

        [Fact]
        public void BuildProjection_Gives_Same_Sql_For_Different_Fields()
        {
            var first = PostgresNoSQLHelper.BuildProjection(["field1"], new NpgsqlCommand().Parameters);
            var second = PostgresNoSQLHelper.BuildProjection(["field7"], new NpgsqlCommand().Parameters);

            Assert.Equal(first, second);
            Assert.DoesNotContain("field", first);
        }

        [Fact]
        public void BuildProjection_Adds_Field_Names_As_Parameters()
        {
            var parameters = new NpgsqlCommand().Parameters;

            PostgresNoSQLHelper.BuildProjection(["field1", "field7"], parameters);

            Assert.Equal(2, parameters.Count);
            Assert.Equal("field1", parameters["f0"].Value);
            Assert.Equal("field7", parameters["f1"].Value);
        }
    }
}
