using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class MsdBucketTests
{
    [Theory]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(1024)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8192)]
    [InlineData(16384)]
    [InlineData(16385)]
    [InlineData(10000)]
    [InlineData(65535)]
    [InlineData(65536)]
    public void DistributionPreservesValuesAndBounds(int count)
    {
        var rng = new Random(count);
        for (int pattern = 0; pattern < 5; pattern++)
        {
            var input = new int[count];
            for (int i = 0; i < count; i++)
                input[i] = pattern switch
                {
                    0 => (int)rng.NextInt64(int.MinValue, (long)int.MaxValue + 1),
                    1 => rng.Next(-16, 16),
                    2 => i % 2 == 0 ? int.MinValue : int.MaxValue,
                    3 => (int)rng.NextInt64(0, 1 << 20),
                    _ => unchecked((int)((uint)rng.Next(256) << 24)) | rng.Next(256)
                };
            Check(input);
            Check(Array.ConvertAll(input, x => unchecked((uint)x)));
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    public void FullAndPartialFixedBuckets(int occupancy)
    {
        // The uppermost bucket includes actual int.MaxValue, not just padding.
        var input = new int[8192];
        var random = new Random(occupancy);
        for (int i = 0; i < input.Length; i++)
        {
            uint bucket = (uint)(i / occupancy) & 4095u;
            if ((i / occupancy & 1) == 0) bucket = 4095u - bucket;
            uint key = (bucket << 20) | (uint)random.Next(1 << 20);
            input[i] = unchecked((int)(key ^ 0x80000000u));
        }
        input[0] = int.MaxValue;
        input[1] = int.MaxValue;
        Check(input);
    }

    private static void Check<T>(T[] input)
    {
        T[] expected = (T[])input.Clone();
        Array.Sort(expected);
        T[] actual = new T[input.Length + 2];
        actual[0] = input[^1];
        actual[^1] = input[0];
        input.CopyTo(actual, 1);
        Sortz.Sort(actual.AsSpan(1, input.Length));
        Assert.Equal(expected, actual[1..^1]);
        Assert.Equal(input[^1], actual[0]);
        Assert.Equal(input[0], actual[^1]);
    }
}
