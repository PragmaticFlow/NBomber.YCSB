using MongoDB.Bson;
using NBomber.YCSB.MongoDb;

namespace NBomber.YCSB.Tests
{
    public class MongoDbHelperTests
    {
        [Fact]
        public void BuildDocument_Is_Flat_With_Id_And_Fields_Only()
        {
            var values = new Dictionary<string, string>
            {
                ["field1"] = "a",
                ["field2"] = null!
            };

            var doc = MongoDbHelper.BuildDocument("user1", values);

            Assert.Equal(new[] { "_id", "field1", "field2" }, doc.Names.ToArray());
            Assert.Equal("user1", doc["_id"].AsString);
            Assert.Equal("a", doc["field1"].AsString);
            Assert.Equal(string.Empty, doc["field2"].AsString);
        }

        [Fact]
        public void BuildScanFilter_Is_Gte_On_Id()
        {
            var filter = MongoDbHelper.BuildScanFilter("user3");

            Assert.Equal(new BsonDocument("_id", new BsonDocument("$gte", "user3")), filter);
        }

        [Fact]
        public void BuildProjection_Null_Or_Empty_Returns_Null()
        {
            Assert.Null(MongoDbHelper.BuildProjection(null));
            Assert.Null(MongoDbHelper.BuildProjection([]));
        }

        [Fact]
        public void BuildProjection_Uses_Plain_Field_Names()
        {
            var projection = MongoDbHelper.BuildProjection(["field3"]);

            Assert.Equal(new BsonDocument("field3", 1), projection);
        }

        [Fact]
        public void BuildUpdate_Sets_Top_Level_Fields_Only()
        {
            var values = new Dictionary<string, string>
            {
                ["field2"] = "x",
                ["field4"] = "y"
            };

            var update = MongoDbHelper.BuildUpdate(values);

            var expected = new BsonDocument("$set", new BsonDocument
            {
                ["field2"] = "x",
                ["field4"] = "y"
            });

            Assert.Equal(expected, update);
        }
    }
}
