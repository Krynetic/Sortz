using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class DecimalPrefixTests
{
    [Theory]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(10000)]
    public void DecimalPowersAndScaleBoundaries(int count)
    {
        var random = new Random(count);
        var values = new decimal[count];
        for (int i = 0; i < count; i++)
        {
            int scale = random.Next(29);
            int power = random.Next(29);
            decimal coefficient = 1m;
            for (int j = 0; j < power; j++) coefficient *= 10m;
            coefficient += random.Next(-1, 2);
            var bits = decimal.GetBits(coefficient);
            values[i] = new decimal(bits[0], bits[1], bits[2], random.Next(2) == 0, (byte)scale);
        }
        var expected = (decimal[])values.Clone();
        Array.Sort(expected);
        Sorter.Sort(values.AsSpan());
        Assert.Equal(expected, values);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(10000)]
    public void Int128PrefixCollisions(int count)
    {
        var random = new Random(count);
        Int128[] values = Enumerable.Range(0, count).Select(i =>
            ((Int128)(i % 5 - 2) << 64) | (ulong)random.NextInt64()).ToArray();
        var expected = (Int128[])values.Clone();
        Array.Sort(expected);
        Sorter.Sort(values.AsSpan());
        Assert.Equal(expected, values);
    }
}
