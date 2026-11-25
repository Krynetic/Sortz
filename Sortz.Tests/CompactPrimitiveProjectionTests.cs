using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class CompactPrimitiveProjectionTests
{
    [Theory]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(10000)]
    [InlineData(65536)]
    [InlineData(65537)]
    public void SignedAndUnsigned128Prefixes(int count)
    {
        var random = new Random(count);
        foreach (int mode in new[] { 0, 1, 2 })
        {
            UInt128[] values = Enumerable.Range(0, count).Select(i => mode switch
            {
                0 => ((UInt128)(ulong)random.NextInt64() << 65) | (ulong)random.NextInt64(),
                1 => ((UInt128)(i % 7) << 96) | (ulong)random.NextInt64(),
                _ => ((UInt128)(i % 2 == 0 ? ulong.MaxValue : 0UL) << 64) | (ulong)random.NextInt64()
            }).ToArray();
            var expected = (UInt128[])values.Clone();
            Array.Sort(expected);
            Sortz.Sort(values.AsSpan());
            Assert.Equal(expected, values);
            var signed = values.Select(x => unchecked((Int128)x)).Reverse().ToArray();
            var signedExpected = (Int128[])signed.Clone();
            Array.Sort(signedExpected);
            Sortz.Sort(signed.AsSpan());
            Assert.Equal(signedExpected, signed);
        }
    }

    [Theory]
    [InlineData(257)]
    [InlineData(1024)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(10000)]
    public void ByteHistogramDoesNotOverwriteAdjacentElements(int count)
    {
        var random = new Random(count);
        var guarded = new byte[count + 2];
        random.NextBytes(guarded);
        byte first = guarded[0], last = guarded[^1];
        byte[] expected = guarded.AsSpan(1, count).ToArray();
        Array.Sort(expected);
        Sortz.Sort(guarded.AsSpan(1, count));
        Assert.Equal(first, guarded[0]);
        Assert.Equal(last, guarded[^1]);
        Assert.Equal(expected, guarded.AsSpan(1, count).ToArray());
    }
}
