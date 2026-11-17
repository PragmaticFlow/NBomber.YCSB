using NBomber.YCSB.Infra;

namespace NBomber.YCSB.Tests
{
    public class ZipfianExtensionsTests
    {
        private const int SEED = 42;
        private const int DRAWS = 20_000;

        private static ulong[] Draw(long itemCount, ulong maxKey, int count = DRAWS)
        {
            var random = new Random(SEED);
            var keys = new ulong[count];

            for (var i = 0; i < count; i++)
                keys[i] = random.ScrambledZipf(itemCount, maxKey);

            return keys;
        }

        [Theory]
        [InlineData(1, 1UL)]         // single item
        [InlineData(2, 1UL)]         // recordcount 1: key 2 is redrawn
        [InlineData(1001, 1000UL)]   // workloads A, B, C, F with recordcount 1000
        [InlineData(1101, 1000UL)]   // workload E before any insert is acknowledged
        [InlineData(1000, 5000UL)]   // maxKey above the item count
        public void Keys_Stay_In_1_To_Min_Of_MaxKey_And_ItemCount(long itemCount, ulong maxKey)
        {
            var upper = Math.Min(maxKey, (ulong)itemCount);

            Assert.All(Draw(itemCount, maxKey), key => Assert.InRange(key, 1UL, upper));
        }

        [Fact]
        public void Skew_Matches_Scrambled_Zipfian_099()
        {
            // YCSB at 1000 records: hottest key ~3.8% (rank 0 = 1/ZETAN), 10 hottest ~12.6%.
            // Unscrambled Zipf 1.3 would give ~28% and ~65%.
            var counts = Draw(1001, 1000).GroupBy(k => k).Select(g => g.Count()).OrderDescending().ToArray();

            var hottestShare = (double)counts[0] / DRAWS;
            var top10Share = (double)counts.Take(10).Sum() / DRAWS;

            Assert.InRange(hottestShare, 0.03, 0.047);
            Assert.InRange(top10Share, 0.10, 0.155);
        }

        [Fact]
        public void Hot_Keys_Are_Spread_Across_The_Key_Space()
        {
            var top10 = Draw(1001, 1000)
                .GroupBy(k => k)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToArray();

            // without scrambling the hottest keys would be 1..10
            Assert.True(top10.Count(k => k <= 100) <= 3, $"hot keys clustered at the start: {string.Join(", ", top10)}");
            Assert.True(top10.Max() - top10.Min() > 500, $"hot keys not spread: {string.Join(", ", top10)}");
        }

        [Fact]
        public void Invalid_Arguments_Are_Rejected()
        {
            var random = new Random(SEED);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.ScrambledZipf(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.ScrambledZipf(1, 0));
        }
    }
}
