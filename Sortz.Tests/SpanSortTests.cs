using Krynetic.Sortz;
using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class SpanSortTests
{
    [Theory]
    [MemberData(nameof(SortConformanceTests.Cases), MemberType = typeof(SortConformanceTests))]
    public void SortsSliceWithoutChangingAdjacentElements(int size, int pattern, bool descending)
    {
        var source = SortInputs.Create(size, pattern);
        var expected = (int[])source.Clone();
        var actual = new int[size + 2];
        actual[0] = 123456;
        actual[^1] = -123456;
        source.AsSpan().CopyTo(actual.AsSpan(1, size));
        IComparer<int>? comparer = descending
            ? Comparer<int>.Create((a, b) => b.CompareTo(a))
            : null;

        Array.Sort(expected, comparer);
        Sortz.Sort(actual.AsSpan(1, size), comparer);

        Assert.Equal(expected, actual.AsSpan(1, size).ToArray());
        Assert.Equal(123456, actual[0]);
        Assert.Equal(-123456, actual[^1]);
    }

    [Fact]
    public void SupportsStackAllocatedSpan()
    {
        Span<int> values = stackalloc int[] { 3, 1, 2, -1, 3 };
        Sortz.Sort(values);
        Assert.Equal(new[] { -1, 1, 2, 3, 3 }, values.ToArray());
    }
}
