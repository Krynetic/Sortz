using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class Mapped64RegressionTests
{
    [Theory]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    public void SmallMappedNetworksPreserveBits(int size)
    {
        for (int seed = 0; seed < 32; seed++)
        {
            var rng = new Random(seed);
            long[] numbers = Enumerable.Range(0, size).Select(_ =>
                rng.NextInt64(long.MinValue, long.MaxValue)).ToArray();
            numbers[0] = long.MinValue;
            numbers[^1] = long.MaxValue;
            Check(numbers);
            Array.Sort(numbers);
            for (int i = 0; i < size; i += 11 + seed % 23)
            {
                int j = rng.Next(size);
                (numbers[i], numbers[j]) = (numbers[j], numbers[i]);
            }
            Check(numbers);

            double[] floating = numbers.Select(BitConverter.Int64BitsToDouble).ToArray();
            floating[0] = double.NegativeInfinity;
            floating[1] = double.PositiveInfinity;
            floating[2] = BitConverter.Int64BitsToDouble(long.MinValue);
            floating[3] = 0;
            if ((seed & 1) == 0)
                floating[4] = BitConverter.Int64BitsToDouble(0x7ff8000000000001L + seed);
            var expected = floating.Select(BitConverter.DoubleToInt64Bits).OrderBy(x => x).ToArray();
            Sortz.Sort(floating.AsSpan());
            Assert.Equal(expected, floating.Select(BitConverter.DoubleToInt64Bits).OrderBy(x => x).ToArray());
            for (int i = 1; i < floating.Length; i++)
                Assert.True(floating[i - 1].CompareTo(floating[i]) <= 0);
        }
    }

    private static void Check<T>(T[] source, IComparer<T>? comparer = null)
    {
        var expected = (T[])source.Clone();
        Array.Sort(expected, comparer);
        // Exercise an offset span and verify adjacent guard elements.
        var actual = new T[source.Length + 2];
        source.CopyTo(actual, 1);
        Sortz.Sort(actual.AsSpan(1, source.Length), comparer);
        Assert.Equal(expected, actual[1..^1]);
        Assert.Equal(default(T), actual[0]);
        Assert.Equal(default(T), actual[^1]);
    }
}
