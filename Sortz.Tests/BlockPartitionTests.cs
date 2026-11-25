using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class BlockPartitionTests
{
    private sealed record Item(int Key, int Id) : IComparable<Item>
    {
        public int CompareTo(Item? other) => other is null ? 1 : Key.CompareTo(other.Key);
    }

    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(193)]
    [InlineData(1024)]
    [InlineData(10000)]
    public void BlockTailsAndDuplicates(int size)
    {
        var random = new Random(size);
        for (int pattern = 0; pattern < 4; pattern++)
        {
            var input = Enumerable.Range(0, size).Select(i => i % 29 == 0 ? null :
                new Item(pattern switch { 0 => random.Next(), 1 => random.Next(3),
                    2 => i % 11 == 0 ? 0 : 1, _ => -i }, i)).ToArray();
            var expected = (Item?[])input.Clone();
            Array.Sort(expected);
            Sorter.Sort(input.AsSpan());
            Assert.Equal(expected.Select(x => x?.Key), input.Select(x => x?.Key));
            Assert.Equal(expected.Select(x => x?.Id).Order(), input.Select(x => x?.Id).Order());
        }
    }
}
