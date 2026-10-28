using NBomber.YCSB.ScyllaDb;

namespace NBomber.YCSB.Tests
{
    public class ScyllaDbHelperTests
    {
        [Fact]
        public void BuildInsertBindings_With_One_Field_Puts_Field1_Value_In_Slot_1()
        {
            var bindings = ScyllaDbHelper.BuildInsertBindings(
                "k", ["field1"], new Dictionary<string, string> { ["field1"] = "v1" });

            Assert.Equal(new object[] { "k", "v1" }, bindings);
        }

        [Fact]
        public void BuildInsertBindings_Maps_Every_Field_To_Its_Column_Slot()
        {
            string[] names = ["field1", "field2", "field3", "field4"];
            var values = names.ToDictionary(n => n, n => $"v-{n}");

            var bindings = ScyllaDbHelper.BuildInsertBindings("k", names, values);

            Assert.Equal(new object[] { "k", "v-field1", "v-field2", "v-field3", "v-field4" }, bindings);
        }

        [Fact]
        public void BuildInsertBindings_Ignores_Dictionary_Order()
        {
            string[] names = ["field1", "field2", "field3"];
            var values = new Dictionary<string, string>
            {
                ["field3"] = "c",
                ["field1"] = "a",
                ["field2"] = "b"
            };

            Assert.Equal(new object[] { "k", "a", "b", "c" }, ScyllaDbHelper.BuildInsertBindings("k", names, values));
        }

        [Fact]
        public void BuildInsertBindings_Missing_Value_Is_Null_In_The_Right_Slot()
        {
            string[] names = ["field1", "field2", "field3"];
            var values = new Dictionary<string, string> { ["field1"] = "a", ["field3"] = "c" };

            var bindings = ScyllaDbHelper.BuildInsertBindings("k", names, values);

            Assert.Equal(4, bindings.Length);
            Assert.Equal("k", bindings[0]);
            Assert.Equal("a", bindings[1]);
            Assert.Null(bindings[2]);
            Assert.Equal("c", bindings[3]);
        }

        [Fact]
        public void BuildInsertBindings_Last_Field_Is_Not_Dropped()
        {
            string[] names = ["field1", "field2"];
            var values = new Dictionary<string, string> { ["field1"] = "a", ["field2"] = "b" };

            var bindings = ScyllaDbHelper.BuildInsertBindings("k", names, values);

            Assert.Equal("b", bindings[^1]);
        }

        [Fact]
        public void BuildUpdateBindings_Follows_Field_List_Order_And_Appends_Key()
        {
            string[] fields = ["field3", "field1"];
            var values = new Dictionary<string, string> { ["field1"] = "a", ["field3"] = "c" };

            var bindings = ScyllaDbHelper.BuildUpdateBindings("k", fields, values);

            Assert.Equal(new object[] { "c", "a", "k" }, bindings);
        }

        [Fact]
        public void BuildUpdateCql_Sets_Only_Requested_Columns()
        {
            var cql = ScyllaDbHelper.BuildUpdateCql(["field2"], "ycsb", "ycsb_table", "ycsb_key");

            Assert.Equal("UPDATE ycsb.ycsb_table SET field2 = ? WHERE ycsb_key = ?", cql);
        }

        [Fact]
        public void BuildUpdateCql_Multiple_Fields_Keep_Order()
        {
            var cql = ScyllaDbHelper.BuildUpdateCql(["field3", "field1"], "ycsb", "ycsb_table", "ycsb_key");

            Assert.Equal("UPDATE ycsb.ycsb_table SET field3 = ?, field1 = ? WHERE ycsb_key = ?", cql);
        }

        [Fact]
        public void BuildProjectedReadCql_Selects_Only_Requested_Columns()
        {
            var cql = ScyllaDbHelper.BuildProjectedReadCql(["field4", "field2"], "ycsb", "ycsb_table", "ycsb_key");

            Assert.Equal("SELECT field4, field2 FROM ycsb.ycsb_table WHERE ycsb_key = ?", cql);
        }

        [Fact]
        public void BuildProjectedScanCql_Puts_Key_First_Then_Requested_Columns()
        {
            var cql = ScyllaDbHelper.BuildProjectedScanCql(["field5"], "ycsb", "ycsb_table", "ycsb_key");

            Assert.Equal(
                "SELECT ycsb_key, field5 FROM ycsb.ycsb_table WHERE TOKEN(ycsb_key) >= TOKEN(?) LIMIT ?",
                cql);
        }

        [Fact]
        public void BuildFieldsKey_Is_Order_Sensitive()
        {
            Assert.Equal("field1,field2", ScyllaDbHelper.BuildFieldsKey(["field1", "field2"]));
            Assert.NotEqual(
                ScyllaDbHelper.BuildFieldsKey(["field1", "field2"]),
                ScyllaDbHelper.BuildFieldsKey(["field2", "field1"]));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(1)]
        public void BuildCreateKeyspaceCql_Uses_Replication_Factor(int rf)
        {
            var cql = ScyllaDbHelper.BuildCreateKeyspaceCql("ycsb", rf);

            Assert.Equal(
                $"CREATE KEYSPACE IF NOT EXISTS ycsb WITH replication = {{'class': 'SimpleStrategy', 'replication_factor': {rf}}}",
                cql);
        }

        [Fact]
        public void GetSize_Sums_Key_Names_And_Values_And_Tolerates_Null()
        {
            var values = new Dictionary<string, string> { ["f1"] = "abc", ["f2"] = null! };

            Assert.Equal(1 + 2 + 3 + 2, ScyllaDbHelper.GetSize("k", values));
            Assert.Equal(1, ScyllaDbHelper.GetSize("k", null));
        }
    }
}
