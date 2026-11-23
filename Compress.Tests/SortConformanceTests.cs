using Compress;
using Xunit;

namespace Compress.Tests;

public sealed class SortConformanceTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (int size in new[] { 0, 1, 2, 3, 15, 16, 17, 31, 32, 63, 64, 127, 128, 1024, 10000 })
        foreach (int pattern in Enumerable.Range(0, 8))
        foreach (bool descending in new[] { false, true })
            yield return new object[] { size, pattern, descending };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void MatchesListSort(int size, int pattern, bool descending)
    {
        var input = SortInputs.Create(size, pattern);
        Check(input, descending ? Comparer<int>.Create((a, b) => b.CompareTo(a)) : null);
    }

    [Fact]
    public void StringsAndNulls()
    {
        Check<string?>([null, "z", "A", "", "a", null], StringComparer.Ordinal);
        Check<string?>([null, "z", "A", "", "a", null]);
    }

    [Fact]
    public void FloatingPointSpecialValues()
    {
        Check<double>([double.NaN, double.NegativeInfinity, -1, -0.0, 0.0, 1, double.PositiveInfinity, double.NaN]);
    }

    [Fact]
    public void CustomTypes()
    {
        Check(Enumerable.Range(0, 100).Reverse().Select(i => new Item(i)), Comparer<Item>.Create((a, b) => a.Key.CompareTo(b.Key)));
    }

    [Fact]
    public void Randomized()
    {
        for (int seed = 0; seed < 100; seed++)
            Check(SortInputs.Create(seed * 19, seed % 8, seed));
    }

    private static void Check<T>(IEnumerable<T> input, IComparer<T>? comparer = null)
    {
        var expected = input.ToList();
        var actual = new List<T>(expected);
        int capacity = actual.Capacity;
        expected.Sort(comparer);
        actual.SortNext(comparer);
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(capacity, actual.Capacity);
        Assert.Equal<T>(expected, actual);
    }

    private sealed record Item(int Key);
}
