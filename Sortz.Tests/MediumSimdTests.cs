using Xunit;

namespace Sortz.Tests;

public sealed class MediumSimdTests
{
    [Theory]
    [InlineData(257)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(1023)]
    [InlineData(1024)]
    public void PreservesBitPatternsAndSortOrder(int count)
    {
        var random = new Random(count);
        double[] input = Enumerable.Range(0, count).Select(i => i % 17 switch
        {
            0 => -0.0, 1 => 0.0, 2 => double.NaN,
            3 => double.NegativeInfinity, 4 => double.PositiveInfinity,
            _ => BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue))
        }).ToArray();
        var expected = (double[])input.Clone();
        Array.Sort(expected);
        SortzNext.Sort(input.AsSpan());
        Assert.Equal(expected, input);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits).Order(),
            input.Select(BitConverter.DoubleToInt64Bits).Order());
    }
}
