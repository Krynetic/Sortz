using System.Runtime.InteropServices;
using Xunit;

namespace Sortz.Tests;

public sealed class PrimitiveSortTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(4095)]
    [InlineData(4096)]
    [InlineData(4097)]
    [InlineData(8193)]
    [InlineData(10000)]
    [InlineData(16384)]
    [InlineData(16385)]
    [InlineData(65535)]
    [InlineData(65536)]
    public void RandomBitPatternsAndCustomComparers(int length)
    {
        var random = new Random(length);
        Check(RandomBits<byte>(random, length));
        Check(RandomBits<sbyte>(random, length));
        Check(RandomBits<char>(random, length));
        Check(RandomBits<ushort>(random, length));
        Check(RandomBits<short>(random, length));
        Check(RandomBits<uint>(random, length));
        Check(RandomBits<long>(random, length));
        Check(RandomBits<ulong>(random, length));
        Check(RandomBits<nint>(random, length));
        Check(RandomBits<nuint>(random, length));
        Check(RandomBits<float>(random, length));
        Check(RandomBits<Half>(random, length));
        Check(RandomBits<Int128>(random, length));
        Check(RandomBits<UInt128>(random, length));
        Check(Enumerable.Range(0, length).Select(_ => random.Next(2) == 0).ToArray());
        Check(Enumerable.Range(0, length).Select(_ => new decimal(random.Next(), random.Next(),
            random.Next(), random.Next(2) == 0, (byte)random.Next(29))).ToArray());
    }

    [Fact]
    public void NarrowRangesAndSignBoundaries()
    {
        var random = new Random(97);
        foreach (int length in new[] { 33, 1025, 10000, 65536 })
        {
            Check(Enumerable.Range(0, length).Select(_ => (ushort)(0xfe00 | random.Next(256))).ToArray());
            Check(Enumerable.Range(0, length).Select(_ => (ushort)((random.Next(256) << 8) | 0xfe)).ToArray());
            Check(Enumerable.Range(0, length).Select(_ => (short)(random.Next(2) == 0 ? short.MinValue : short.MaxValue)).ToArray());
            Check(Enumerable.Range(0, length).Select(_ => random.Next(2) == 0 ? UInt128.MinValue : UInt128.MaxValue).ToArray());
            Check(Enumerable.Range(0, length).Select(_ => random.Next(2) == 0 ? Int128.MinValue : Int128.MaxValue).ToArray());
        }
    }

    [Fact]
    public void FloatingPointPayloadsAndDecimalRepresentations()
    {
        uint[] single = [0u, 0x80000000u, 0x7f800000u, 0xff800000u, 0x7fc00001u,
            0xffc00002u, 1u, 0x80000001u, 0x7f800001u];
        ushort[] half = [0, 0x8000, 0x7c00, 0xfc00, 0x7e01, 0xfe02, 1, 0x8001, 0x7c01];
        foreach (int length in new[] { 33, 128, 1024, 10000 })
        {
            Check(Enumerable.Range(0, length).Select(i => BitConverter.UInt32BitsToSingle(single[i % single.Length])).ToArray());
            Check(Enumerable.Range(0, length).Select(i => BitConverter.UInt16BitsToHalf(half[i % half.Length])).ToArray());
            decimal[] representations = [0m, new decimal(0, 0, 0, true, 28), 1m, 1.0m, 1.00m, -1m,
                decimal.MinValue, decimal.MaxValue, 0.0000000000000000000000000001m];
            Check(Enumerable.Range(0, length).Select(i => representations[i % representations.Length]).ToArray());
        }
    }

    private static T[] RandomBits<T>(Random random, int length) where T : unmanaged
    {
        var values = new T[length];
        random.NextBytes(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }

    private static void Check<T>(T[] source) where T : unmanaged
    {
        var comparer = Comparer<T>.Default;
        var expected = (T[])source.Clone();
        Array.Sort(expected, comparer);

        var actual = (T[])source.Clone();
        SortzNext.Sort(actual.AsSpan());
        AssertOrdered<T>(expected, actual, comparer);
        AssertSameBits<T>(source, actual);

        // Explicit default comparer and non-default ordering on a guarded slice.
        var guarded = new T[source.Length + 2];
        guarded[0] = source.Length == 0 ? default : source[0];
        guarded[^1] = source.Length == 0 ? default : source[^1];
        source.AsSpan().CopyTo(guarded.AsSpan(1, source.Length));
        T left = guarded[0], right = guarded[^1];
        SortzNext.Sort(guarded.AsSpan(1, source.Length), comparer);
        Assert.Equal(left, guarded[0]);
        Assert.Equal(right, guarded[^1]);
        AssertOrdered<T>(expected, guarded.AsSpan(1, source.Length), comparer);
        AssertSameBits<T>(source, guarded.AsSpan(1, source.Length));

        var reverse = Comparer<T>.Create((x, y) => comparer.Compare(y, x));
        Array.Sort(expected, reverse);
        SortzNext.Sort(actual.AsSpan(), reverse);
        AssertOrdered<T>(expected, actual, reverse);
        AssertSameBits<T>(source, actual);

        // Ordered and reverse-ordered fast paths.
        SortzNext.Sort(actual.AsSpan());
        Array.Reverse(expected);
        AssertOrdered<T>(expected, actual, comparer);
        SortzNext.Sort(actual.AsSpan());
        AssertOrdered<T>(expected, actual, comparer);
    }

    private static void AssertOrdered<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual, IComparer<T> comparer)
    {
        for (int i = 0; i < expected.Length; i++)
            Assert.Equal(0, comparer.Compare(expected[i], actual[i]));
    }

    private static void AssertSameBits<T>(ReadOnlySpan<T> source, ReadOnlySpan<T> actual) where T : unmanaged
    {
        // Compare each original element encoding, not merely numerical equality.
        static string[] Encodings(ReadOnlySpan<T> values)
        {
            string[] result = new string[values.Length];
            var bytes = MemoryMarshal.AsBytes(values);
            int size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToHexString(bytes.Slice(i * size, size));
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }
        Assert.Equal(Encodings(source), Encodings(actual));
    }
}
