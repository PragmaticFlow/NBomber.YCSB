namespace NBomber.YCSB.Tests
{
    public static class TestsHelper
    {
        public static int RoundToNearest(int value, int nearest)
        {
            return (int)Math.Round(value / (double)nearest) * nearest;
        }

        public static async Task RetryAsync(Func<Task> test, int retryCount = 3, int delayMs = 500)
        {
            for (var attempt = 0; attempt < retryCount; attempt++)
            {
                try
                {
                    await test();
                    return;
                }
                catch when (attempt < retryCount - 1)
                {
                    await Task.Delay(delayMs);
                }
            }
        }
    }
}
