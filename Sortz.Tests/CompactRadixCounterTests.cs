using Xunit;

namespace Sortz.Tests;

public sealed class CompactRadixCounterTests
{
    [Theory]
    [InlineData(4096)]
    [InlineData(16384)]
    [InlineData(16385)]
    [InlineData(65535)]
    [InlineData(65536)]
    public void CounterLimitsAndUniformDigits(int count)
    {
        var random = new Random(count);
        for (int pattern = 0; pattern < 3; pattern++)
        {
            int[] input = Enumerable.Range(0, count).Select(i => pattern switch
            {
                0 => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
                1 => random.Next(2) << 25,
                _ => i == count / 2 ? int.MinValue : int.MaxValue
            }).ToArray();
            var expected = (int[])input.Clone();
            Array.Sort(expected);
            SortzNext.Sort(input.AsSpan());
            Assert.Equal(expected, input);
        }
    }
}
