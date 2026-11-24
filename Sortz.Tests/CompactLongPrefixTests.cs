using Xunit;

namespace Sortz.Tests;

public sealed class CompactLongPrefixTests
{
    [Theory]
    [InlineData(4097)]
    [InlineData(10000)]
    [InlineData(65535)]
    public void NarrowEightCharacterPrefixesAndCollisions(int count)
    {
        var random = new Random(count);
        for (int mode = 0; mode < 3; mode++)
        {
            string?[] input = Enumerable.Range(0, count).Select(i => mode switch
            {
                0 => random.NextInt64().ToString("X16"),
                1 => "ABCDEF01" + random.Next(256).ToString("X4"),
                _ => i % 31 == 0 ? null : i % 19 == 0 ? "€" : random.NextInt64().ToString("X16")
            }).ToArray();
            var expected = (string?[])input.Clone();
            Array.Sort(expected, StringComparer.Ordinal);
            SortzNext.Sort(input.AsSpan(), StringComparer.Ordinal);
            Assert.Equal(expected, input);
        }
    }
}
