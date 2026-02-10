namespace NBomber.YCSB.Tests
{
    public static class TestsHelper
    {
        public static int RoundToNearest(int value, int nearest)
        {
            return (int)Math.Round(value / (double)nearest) * nearest;
        }
    }
}
