using Xunit;

namespace Sortz.Tests;

public sealed class SimdSortTests
{
    [Fact]
    public void AllSmallSizesAndSeeds()
    {
        for (int size = 0; size <= 256; size++)
        for (int seed = 0; seed < 8; seed++)
        {
            int[] actual = SortInputs.Create(size, seed % 8, seed);
            int[] expected = (int[])actual.Clone();
            Array.Sort(expected);
            SortzNext.Sort(actual.AsSpan());
            Assert.Equal(expected, actual);

            double[] doubles = actual.Select((x, i) => (double)x / (i + 1)).Reverse().ToArray();
            double[] sorted = (double[])doubles.Clone();
            Array.Sort(sorted);
            SortzNext.Sort(doubles.AsSpan());
            Assert.Equal(sorted, doubles);
        }
    }
}
