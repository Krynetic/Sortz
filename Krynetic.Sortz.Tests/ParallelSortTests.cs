using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class ParallelSortTests
{
    [Theory]
    [InlineData(8191)]
    [InlineData(8192)]
    [InlineData(10000)]
    [InlineData(10001)]
    [InlineData(32771)]
    public void ComparableReferencesPreserveOrderAndIdentity(int count)
    {
        var random = new Random(count);
        var values = Enumerable.Range(0, count)
            .Select(i => i % 17 == 0 ? null : new Item(random.Next(32), i)).ToArray();
        Check(values);
        Check(Enumerable.Range(0, count).Select(i => new Item(random.Next(), i)).ToArray());
        Check(Enumerable.Range(0, count).Select(i => new Item(i, i)).ToArray());
        Check(Enumerable.Range(0, count).Select(i => new Item(count - i, i)).ToArray());
    }

    [Fact]
    public void CustomReferenceComparerRemainsSequential()
    {
        int thread = Environment.CurrentManagedThreadId;
        var random = new Random(7);
        var values = Enumerable.Range(0, 10001).Select(i => new Item(random.Next(), i)).ToArray();
        var comparer = Comparer<Item>.Create((x, y) =>
        {
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            return y.Key.CompareTo(x.Key);
        });
        var expected = values.ToArray();
        Array.Sort(expected, comparer);
        Sortz.Sort(values.AsSpan(), comparer);
        Assert.Equal(expected.Select(x => x.Key), values.Select(x => x.Key));
        Assert.Equal(expected.Select(x => x.Id).Order(), values.Select(x => x.Id).Order());
    }

    [Fact]
    public void ConcurrentSlicesPreserveBoundaries()
    {
        Parallel.For(0, 4, worker =>
        {
            var random = new Random(worker + 11);
            var values = Enumerable.Range(0, 131075).Select(i => new Item(random.Next(100), i)).ToArray();
            Item first = values[0], last = values[^1];
            var expected = values.AsSpan(1, values.Length - 2).ToArray();
            Array.Sort(expected);
            Sortz.Sort(values.AsSpan(1, values.Length - 2));
            Assert.Same(first, values[0]);
            Assert.Same(last, values[^1]);
            Assert.Equal(expected.Select(x => x.Key), values.Skip(1).SkipLast(1).Select(x => x.Key));
            Assert.Equal(expected.Select(x => x.Id).Order(), values.Skip(1).SkipLast(1).Select(x => x.Id).Order());
        });
    }

    private static void Check(Item?[] values)
    {
        var expected = values.ToArray();
        Array.Sort(expected);
        Sortz.Sort(values.AsSpan());
        Assert.Equal(expected.Select(x => x?.Key), values.Select(x => x?.Key));
        Assert.Equal(expected.Where(x => x is not null).Select(x => x!.Id).Order(),
            values.Where(x => x is not null).Select(x => x!.Id).Order());
    }

    private sealed record Item(int Key, int Id) : IComparable<Item>
    {
        public int CompareTo(Item? other) => other is null ? 1 : Key.CompareTo(other.Key);
    }
}
