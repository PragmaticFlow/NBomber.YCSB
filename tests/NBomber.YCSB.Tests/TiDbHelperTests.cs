using NBomber.YCSB.Infra;
using NBomber.YCSB.TiDb;

namespace NBomber.YCSB.Tests
{
    public class TiDbHelperTests
    {
        [Fact]
        public void BuildConnectionString_Uses_Defaults()
        {
            var cs = TiDbHelper.BuildConnectionString([]);

            Assert.Contains("Server=localhost", cs);
            Assert.Contains("Port=4000", cs);
            Assert.Contains("User ID=root", cs);
            Assert.DoesNotContain("Database=", cs);
        }

        [Fact]
        public void BuildConnectionString_Reads_Props()
        {
            var props = YcsbCliArgs.ParseProps(["tidb.host=db1", "tidb.port=4001", "tidb.user=bench", "tidb.password=pw"]);

            var cs = TiDbHelper.BuildConnectionString(props);

            Assert.Contains("Server=db1", cs);
            Assert.Contains("Port=4001", cs);
            Assert.Contains("User ID=bench", cs);
            Assert.Contains("Password=pw", cs);
        }

        [Fact]
        public void GetDatabase_Defaults_To_Ycsb_And_Reads_Prop()
        {
            Assert.Equal("ycsb", TiDbHelper.GetDatabase([]));
            Assert.Equal("other", TiDbHelper.GetDatabase(YcsbCliArgs.ParseProps(["tidb.database=other"])));
        }

        [Theory]
        [InlineData("ycsb; DROP TABLE x")]
        [InlineData("a.b")]
        [InlineData("a-b")]
        [InlineData("`x`")]
        public void GetDatabase_Rejects_Names_That_Are_Not_Plain_Identifiers(string name)
        {
            var props = new Dictionary<string, string> { ["tidb.database"] = name };

            Assert.Throws<ArgumentException>(() => TiDbHelper.GetDatabase(props));
        }

        [Fact]
        public void BuildCreateTableSql_Has_Clustered_Binary_Key_And_One_Text_Column_Per_Field()
        {
            var sql = TiDbHelper.BuildCreateTableSql("ycsb", ["field1", "field2"]);

            Assert.Equal(
                "CREATE TABLE IF NOT EXISTS ycsb.ycsb_table " +
                "(ycsb_key VARCHAR(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin NOT NULL, field1 TEXT, field2 TEXT, " +
                "PRIMARY KEY (ycsb_key) CLUSTERED) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin",
                sql);
        }

        [Fact]
        public void BuildDropAndCreateDatabaseSql_Use_Qualified_Names()
        {
            Assert.Equal("DROP TABLE IF EXISTS ycsb.ycsb_table", TiDbHelper.BuildDropTableSql("ycsb"));
            Assert.Equal("CREATE DATABASE IF NOT EXISTS ycsb", TiDbHelper.BuildCreateDatabaseSql("ycsb"));
        }

        [Fact]
        public void BuildInsertSql_Has_Key_Plus_One_Placeholder_Per_Field_And_Row()
        {
            Assert.Equal(
                "INSERT INTO ycsb.ycsb_table (ycsb_key, field1, field2) VALUES (?, ?, ?)",
                TiDbHelper.BuildInsertSql("ycsb", ["field1", "field2"], rowCount: 1));

            Assert.Equal(
                "INSERT INTO ycsb.ycsb_table (ycsb_key, field1) VALUES (?, ?), (?, ?), (?, ?)",
                TiDbHelper.BuildInsertSql("ycsb", ["field1"], rowCount: 3));
        }

        [Fact]
        public void BuildReadSql_Selects_Requested_Columns_In_Order()
        {
            Assert.Equal(
                "SELECT field4, field2 FROM ycsb.ycsb_table WHERE ycsb_key = ?",
                TiDbHelper.BuildReadSql("ycsb", ["field4", "field2"]));
        }

        [Fact]
        public void BuildUpdateSql_Sets_Only_Requested_Columns_And_Keeps_Order()
        {
            Assert.Equal(
                "UPDATE ycsb.ycsb_table SET field3 = ?, field1 = ? WHERE ycsb_key = ?",
                TiDbHelper.BuildUpdateSql("ycsb", ["field3", "field1"]));
        }

        [Fact]
        public void BuildScanSql_Puts_Key_First_And_Orders_By_Key()
        {
            Assert.Equal(
                "SELECT ycsb_key, field5 FROM ycsb.ycsb_table WHERE ycsb_key >= ? ORDER BY ycsb_key LIMIT ?",
                TiDbHelper.BuildScanSql("ycsb", ["field5"]));
        }

        [Fact]
        public void GetSize_Sums_Key_Names_And_Values_In_Utf8_Bytes_And_Tolerates_Null()
        {
            var values = new Dictionary<string, string> { ["f1"] = "abc", ["f2"] = null!, ["f3"] = "é" };

            Assert.Equal(1 + 2 + 3 + 2 + 2 + 2, TiDbHelper.GetSize("k", values));
            Assert.Equal(1, TiDbHelper.GetSize("k", null));
        }
    }
}
