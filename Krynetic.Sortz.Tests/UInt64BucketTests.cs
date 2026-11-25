using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class UInt64BucketTests
{
    [Theory]
    [InlineData(65537)]
    [InlineData(100003)]
    [InlineData(262144)]
    public void SignedBucketRotationPreservesOffsetSpanAndExtremes(int count)
    {
        var random = new Random(count);
        foreach (bool skewed in new[] { false, true })
        {
            var values = new long[count + 4];
            values[0] = values[1] = long.MaxValue;
            values[^1] = values[^2] = long.MinValue;
            for (int i = 0; i < count; i++)
                values[i + 2] = skewed
                    ? random.Next(-16, 17)
                    : random.NextInt64(long.MinValue, long.MaxValue);
            values[2] = long.MinValue;
            values[3] = long.MaxValue;
            values[4] = 0;
            values[5] = -1;
            long[] expected = (long[])values.Clone();
            Array.Sort(expected, 2, count);
            Sortz.Sort(values.AsSpan(2, count));
            Assert.Equal(expected, values);
        }
    }

    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(8192)]
    [InlineData(8193)]
    [InlineData(10000)]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(100003)]
    [InlineData(262144)]
    [InlineData(262145)]
    public void FullRangeAndSkewedBuckets(int count)
    {
        var random = new Random(count);
        foreach (int pattern in new[] { 0, 1, 2, 3 })
        {
            ulong[] values = Enumerable.Range(0, count).Select(i => pattern switch
            {
                0 => ((ulong)random.NextInt64() << 1) | (uint)random.Next(2),
                1 => (ulong)random.Next(32),
                2 => i % 3 == 0 ? ulong.MaxValue : (ulong)random.Next(),
                _ => ((ulong)(i & 1023) << 54) | (ulong)random.Next(16)
            }).ToArray();
            var expected = (ulong[])values.Clone();
            Array.Sort(expected);
            Sortz.Sort(values.AsSpan());
            Assert.Equal(expected, values);

            long[] signed = values.Select(x => unchecked((long)x)).Reverse().ToArray();
            var sorted = (long[])signed.Clone();
            Array.Sort(sorted);
            Sortz.Sort(signed.AsSpan());
            Assert.Equal(sorted, signed);
        }
    }
}
