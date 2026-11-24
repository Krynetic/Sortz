using Xunit;

namespace Sortz.Tests;

public sealed class FloatingKeyRotationTests
{
    [Fact]
    public void ExhaustiveHalfBitPatterns()
    {
        var bits = Enumerable.Range(0, 65536).Select(i => (ushort)i).ToArray();
        new Random(423).Shuffle(bits.AsSpan());
        Half[] values = bits.Select(BitConverter.UInt16BitsToHalf).ToArray();
        var expected = (Half[])values.Clone();
        Array.Sort(expected);
        SortzNext.Sort(values.AsSpan());
        for (int i = 0; i < values.Length; i++)
            Assert.Equal(0, expected[i].CompareTo(values[i]));
        Assert.Equal(Enumerable.Range(0, 65536).Select(i => (ushort)i),
            values.Select(BitConverter.HalfToUInt16Bits).Order());
    }

    [Theory]
    [InlineData(33)]
    [InlineData(128)]
    [InlineData(1025)]
    [InlineData(10000)]
    public void SingleNaNBoundaryRotations(int length)
    {
        uint[] patterns = [0, 0x80000000, 1, 0x80000001, 0x7f7fffff, 0xff7fffff,
            0x7f800000, 0xff800000, 0x7f800001, 0xff800001, 0x7fffffff, 0xffffffff,
            0x7fc12345, 0xffc12345];
        var random = new Random(length);
        var bits = Enumerable.Range(0, length).Select(_ => patterns[random.Next(patterns.Length)]).ToArray();
        float[] values = bits.Select(BitConverter.UInt32BitsToSingle).ToArray();
        var expected = (float[])values.Clone();
        Array.Sort(expected);
        SortzNext.Sort(values.AsSpan());
        for (int i = 0; i < length; i++) Assert.Equal(0, expected[i].CompareTo(values[i]));
        Assert.Equal(bits.Order(), values.Select(BitConverter.SingleToUInt32Bits).Order());
    }
}
