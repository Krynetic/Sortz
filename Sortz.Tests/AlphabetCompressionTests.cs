using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class AlphabetCompressionTests
{
    [Theory]
    [InlineData(4097)]
    [InlineData(10000)]
    [InlineData(32768)]
    public void ArbitrarySparseAlphabetAndPrefixCollisions(int count)
    {
        const string alphabet = " !#07AZaz~\0\t";
        var random = new Random(count);
        var values = Enumerable.Range(0, count).Select(_ =>
            new string(Enumerable.Range(0, 4).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()) +
            random.Next(100).ToString()).ToArray();
        var expected = (string[])values.Clone();
        Array.Sort(expected, StringComparer.Ordinal);
        Sorter.Sort(values.AsSpan(), StringComparer.Ordinal);
        Assert.Equal(expected, values);
    }
}
