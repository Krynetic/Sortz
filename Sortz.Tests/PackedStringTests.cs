using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class PackedStringTests
{
    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(8193)]
    [InlineData(10000)]
    public void PreservesAllOrdinalCollisionsAndUtf16Ordering(int count)
    {
        var random = new Random(111);
        string?[] prefixes = [null, "", "\0", "abcdef", "abcdef\0", "abcdef\0z",
            "abcdefgh", "abcdefghi", "abcdef\ud800", "abcdef\udfff", "😀", "é", "a\0b"];
        string?[] values = Enumerable.Range(0, count).Select(i =>
            i % 3 == 0 ? prefixes[random.Next(prefixes.Length)] :
                "abcdef" + (char)random.Next(65536) + random.Next(256).ToString("X2")).ToArray();
        var expected = (string?[])values.Clone();
        Array.Sort(expected, StringComparer.Ordinal);
        Sortz.Sort(values.AsSpan(), StringComparer.Ordinal);
        Assert.Equal(expected, values);
    }
}
