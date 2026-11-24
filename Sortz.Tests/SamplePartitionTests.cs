using Xunit;

namespace Sortz.Tests;

public sealed class SamplePartitionTests
{
    private sealed record Item(int Key, int Id) : IComparable<Item>
    {
        public int CompareTo(Item? other) => other is null ? 1 : Key.CompareTo(other.Key);
    }

    [Theory]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(1024)]
    [InlineData(10000)]
    [InlineData(16385)]
    public void PreservesReferencesAndOrder(int size)
    {
        var r = new Random(size);
        for (int distribution = 0; distribution < 3; distribution++)
        {
            var values = Enumerable.Range(0, size).Select(i => i % 19 == 0 ? null :
                new Item(distribution == 0 ? r.Next() : distribution == 1 ? r.Next(4) : i % 100 == 0 ? r.Next() : 1, i)).ToArray();
            var expected = (Item?[])values.Clone();
            Array.Sort(expected);
            SortzNext.Sort(values.AsSpan());
            Assert.Equal(expected.Select(x => x?.Key), values.Select(x => x?.Key));
            Assert.Equal(expected.Select(x => x?.Id).Order(), values.Select(x => x?.Id).Order());
        }
    }
}
