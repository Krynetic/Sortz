using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class CompactPrefixTests
{
    [Theory]
    [InlineData(64, 32, 64)]
    [InlineData(128, 48, 23)]
    [InlineData(1024, 48, 23)]
    [InlineData(4096, 48, 12)]
    [InlineData(1024, 0x400, 12)]
    public void NarrowAlphabetAndPrefixCollisions(int count, int first, int alphabet)
    {
        var random = new Random(count);
        var values = Enumerable.Range(0, count)
            .Select(i => new string(Enumerable.Range(0, 4)
                .Select(_ => (char)(first + random.Next(alphabet))).ToArray()) +
                random.Next(5).ToString()).ToArray();
        var expected = (string[])values.Clone();
        Array.Sort(expected, StringComparer.Ordinal);
        Sortz.Sort(values.AsSpan(), StringComparer.Ordinal);
        Assert.Equal(expected, values);
    }
}
