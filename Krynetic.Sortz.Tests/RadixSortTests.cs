using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class RadixSortTests
{
    [Theory]
    [InlineData(33)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(10000)]
    [InlineData(16384)]
    [InlineData(16385)]
    [InlineData(100000)]
    [InlineData(1000000)]
    public void FullSignedRange(int size)
    {
        for (int seed = 0; seed < 3; seed++)
        {
            var actual = SortInputs.Create(size, 0, seed);
            var expected = (int[])actual.Clone();
            Array.Sort(expected);
            Sortz.Sort(actual.AsSpan(), Comparer<int>.Default);
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    public void SingleVaryingDigit(int shift)
    {
        var random = new Random(100);
        int[] actual = Enumerable.Range(0, 20000)
            .Select(_ => random.Next(256) << shift).ToArray();
        var expected = (int[])actual.Clone();
        Array.Sort(expected);
        Sortz.Sort(actual.AsSpan());
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void LargePatterns(int pattern)
    {
        var actual = SortInputs.Create(100000, pattern);
        var expected = (int[])actual.Clone();
        Array.Sort(expected);
        Sortz.Sort(actual.AsSpan());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CustomComparerStillControlsOrder()
    {
        var comparer = Comparer<int>.Create((a, b) => b.CompareTo(a));
        var actual = SortInputs.Create(10000, 0);
        var expected = (int[])actual.Clone();
        Array.Sort(expected, comparer);
        Sortz.Sort(actual.AsSpan(), comparer);
        Assert.Equal(expected, actual);
    }
}
