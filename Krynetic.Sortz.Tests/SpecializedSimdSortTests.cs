using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class SpecializedSimdSortTests
{
    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(256)]
    public void ByteAndSignedBytePreserveOrderAndSliceBounds(int count)
    {
        var random = new Random(count);
        byte[] bytes = new byte[count];
        for (int iteration = 0; iteration < 16; iteration++)
        {
            random.NextBytes(bytes);
            if (iteration == 1) Array.Fill(bytes, byte.MaxValue);
            if (iteration == 2) Array.Sort(bytes);
            if (iteration == 3) { Array.Sort(bytes); Array.Reverse(bytes); }

            CheckSlice(bytes, (byte)42, (byte)99);
            CheckSlice(bytes.Select(x => unchecked((sbyte)x)).ToArray(), (sbyte)-37, (sbyte)91);
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(33)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(10000)]
    public void WideIntegersHandleEqualHighHalvesAndSignBoundaries(int count)
    {
        var random = new Random(count);
        var unsigned = Enumerable.Range(0, count).Select(i =>
            ((UInt128)(ulong)(i % 5 == 0 ? 0 : i % 3 == 0 ? long.MaxValue : long.MinValue) << 64)
            | (ulong)random.NextInt64()).ToArray();

        CheckPatterns(unsigned, UInt128.MaxValue, UInt128.Zero);
        CheckPatterns(unsigned.Select(x => unchecked((Int128)x)).ToArray(), Int128.MinValue, Int128.MaxValue);
    }

    [Theory]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(1024)]
    public void HalfHandlesNaNsSignedZerosAndInfinity(int count)
    {
        var random = new Random(count);
        var values = Enumerable.Range(0, count)
            .Select(_ => BitConverter.UInt16BitsToHalf((ushort)random.Next(65536))).ToArray();
        ushort[] special = [0, 0x8000, 0x7c00, 0xfc00, 0x7e01, 0xfe01, 0x7bff, 0xfbff];
        for (int i = 0; i < special.Length; i++)
            values[i] = BitConverter.UInt16BitsToHalf(special[i]);

        for (int pattern = 0; pattern < 4; pattern++)
        {
            var input = values.ToArray();
            if (pattern != 0) Array.Sort(input);
            if (pattern == 2) Array.Reverse(input);
            if (pattern == 3) (input[0], input[^1]) = (input[^1], input[0]);

            var expected = input.ToArray();
            Array.Sort(expected);
            var bits = input.Select(BitConverter.HalfToUInt16Bits).Order().ToArray();
            Sortz.Sort(input.AsSpan());
            for (int i = 0; i < input.Length; i++)
                Assert.Equal(0, expected[i].CompareTo(input[i]));
            Assert.Equal(bits, input.Select(BitConverter.HalfToUInt16Bits).Order().ToArray());
        }
    }

    [Theory]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(10000)]
    public void SparseRepairHandlesNarrowNativeAndWideKeys(int count)
    {
        var values = Enumerable.Range(0, count).Select(i => (i * 3) - count).ToArray();
        for (int i = 31; i < count; i += 97)
            (values[i], values[i - 23]) = (values[i - 23], values[i]);

        CheckSlice(values.Select(x => (short)x).ToArray(), short.MinValue, short.MaxValue);
        CheckSlice(values.Select(x => (ushort)(x + count)).ToArray(), ushort.MinValue, ushort.MaxValue);
        CheckSlice(values.Select(x => (nint)x).ToArray(), (nint)(-1), (nint)1);
        CheckSlice(values.Select(x => (nuint)(x + count)).ToArray(), (nuint)0, (nuint)1);
        CheckSlice(values.Select(x => (Int128)x << 64).ToArray(), Int128.MinValue, Int128.MaxValue);
        CheckSlice(values.Select(x => (UInt128)(x + count) << 64).ToArray(), UInt128.MinValue, UInt128.MaxValue);
    }

    [Theory]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    public void FusedSignNetworksPreserveFullWidthKeys(int count)
    {
        var random = new Random(count);
        byte[] raw = new byte[count * sizeof(long)];
        for (int iteration = 0; iteration < 16; iteration++)
        {
            random.NextBytes(raw);
            var signed16 = new short[count];
            var unsigned32 = new uint[count];
            var signed64 = new long[count];
            for (int i = 0; i < count; i++)
            {
                signed16[i] = BitConverter.ToInt16(raw, i * sizeof(short));
                unsigned32[i] = BitConverter.ToUInt32(raw, i * sizeof(uint));
                signed64[i] = BitConverter.ToInt64(raw, i * sizeof(long));
            }
            signed16[0] = short.MinValue; signed16[1] = short.MaxValue;
            unsigned32[0] = uint.MinValue; unsigned32[1] = uint.MaxValue;
            signed64[0] = long.MinValue; signed64[1] = long.MaxValue;
            signed16[2] = signed16[3] = 0;
            unsigned32[2] = unsigned32[3] = 0x80000000u;
            signed64[2] = signed64[3] = -1;
            CheckSlice(signed16, (short)-7, (short)9);
            CheckSlice(unsigned32, 7u, 9u);
            CheckSlice(signed64, -7L, 9L);
            CheckSlice(signed64.Select(x => unchecked((nint)x)).ToArray(), (nint)(-7), (nint)9);
        }
    }

    private static void CheckPatterns<T>(T[] input, T before, T after)
    {
        CheckSlice(input, before, after);
        Array.Sort(input);
        CheckSlice(input, before, after);
        Array.Reverse(input);
        CheckSlice(input, before, after);
        (input[0], input[^1]) = (input[^1], input[0]);
        CheckSlice(input, before, after);
    }

    private static void CheckSlice<T>(T[] input, T before, T after)
    {
        var expected = input.ToArray();
        Array.Sort(expected);
        T[] actual = new T[input.Length + 2];
        actual[0] = before;
        actual[^1] = after;
        input.CopyTo(actual, 1);
        Sortz.Sort(actual.AsSpan(1, input.Length));
        Assert.Equal(before, actual[0]);
        Assert.Equal(after, actual[^1]);
        Assert.Equal(expected, actual.AsSpan(1, input.Length).ToArray());
    }
}
