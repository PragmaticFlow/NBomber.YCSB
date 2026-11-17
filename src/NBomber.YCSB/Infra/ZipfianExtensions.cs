namespace NBomber.YCSB.Infra;

public static class ZipfianExtensions
{
    private const double ZIPFIAN_CONSTANT = 0.99;
    private const double ZETAN = 26.46902820178302;              // zeta(10^10, 0.99), YCSB ScrambledZipfianGenerator.ZETAN
    private const long ZIPFIAN_ITEMS = 10_000_000_001L;          // YCSB ITEM_COUNT + 1 (items = max - min + 1)
    private const long FNV_OFFSET_BASIS_64 = unchecked((long)0xCBF29CE484222325);
    private const long FNV_PRIME_64 = 1099511628211L;

    private static readonly double ZIPFIAN_ZETA_2_THETA = 1 + 1 / Math.Pow(2, ZIPFIAN_CONSTANT);   // Java zeta(2, theta)
    private static readonly double ZIPFIAN_RANK_1_LIMIT = 1.0 + Math.Pow(0.5, ZIPFIAN_CONSTANT);   // nextLong branch threshold
    private static readonly double ZIPFIAN_ALPHA = 1.0 / (1.0 - ZIPFIAN_CONSTANT);
    private static readonly double ZIPFIAN_ETA =
        (1 - Math.Pow(2.0 / ZIPFIAN_ITEMS, 1 - ZIPFIAN_CONSTANT)) / (1 - ZIPFIAN_ZETA_2_THETA / ZETAN);

    /// <summary>
    /// Draws a scrambled zipfian key number in [1, <paramref name="maxKey"/>]. Keys above <paramref name="maxKey"/> are redrawn.
    /// </summary>
    /// <param name="itemCount">Size of the key range the scrambled rank is mapped onto (recordcount + expected new keys + 1).</param>
    /// <param name="maxKey">Highest key number that may be returned.</param>
    public static ulong ScrambledZipf(this Random random, long itemCount, ulong maxKey)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(itemCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxKey, 1UL);

        while (true)
        {
            var hash = FnvHash64(NextRank(random));

            if (hash == long.MinValue)
                continue;

            var keyNum = 1 + Math.Abs(hash) % itemCount;

            if ((ulong)keyNum <= maxKey)
                return (ulong)keyNum;
        }
    }

    private static long NextRank(Random random)
    {
        var u = random.NextDouble();
        var uz = u * ZETAN;

        if (uz < 1.0)
            return 0;

        if (uz < ZIPFIAN_RANK_1_LIMIT)
            return 1;

        return (long)(ZIPFIAN_ITEMS * Math.Pow(ZIPFIAN_ETA * u - ZIPFIAN_ETA + 1, ZIPFIAN_ALPHA));
    }

    private static long FnvHash64(long value)
    {
        var hash = FNV_OFFSET_BASIS_64;

        for (var i = 0; i < 8; i++)
        {
            var octet = value & 0xff;
            value >>= 8;
            hash ^= octet;
            hash = unchecked(hash * FNV_PRIME_64);
        }

        return hash;
    }
}
