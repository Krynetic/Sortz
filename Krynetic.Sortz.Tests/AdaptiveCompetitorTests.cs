using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class AdaptiveCompetitorTests
{
    [Theory]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(127)]
    [InlineData(257)]
    [InlineData(1025)]
    [InlineData(4097)]
    [InlineData(16385)]
    [InlineData(100003)]
    public void AdaptivePathsPreservePermutation(int size)
    {
        var random = new Random(size);
        for (int pattern = 0; pattern < 7; pattern++)
        {
            long[] source = Enumerable.Range(0, size).Select(i => (long)(i / 3) - size).ToArray();
            switch (pattern)
            {
                case 1: Array.Reverse(source); break;
                case 2: Array.Fill(source, 42L); break;
                case 3:
                    for (int i = 0; i < size; i += 37)
                    {
                        int j = random.Next(size);
                        (source[i], source[j]) = (source[j], source[i]);
                    }
                    break;
                case 4:
                    // Ordered sample followed by disorder: force the bounded repair fallback.
                    for (int i = 17; i < size; i++) source[i] = random.NextInt64();
                    break;
                case 5:
                    for (int i = 0; i < size; i++) source[i] = random.NextInt64();
                    break;
                case 6:
                    source[0] = long.MaxValue;
                    source[^1] = long.MinValue;
                    break;
            }
            Check(source);
            Check(source.Select(x => unchecked((int)x)).ToArray());
            Check(source.Select(x => (double)x).ToArray());
            Check(source.Select(x => x.ToString("D20", System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
                StringComparer.Ordinal);
        }
    }

    [Theory]
    [InlineData(257)]
    [InlineData(4097)]
    [InlineData(100003)]
    public void SparseMergeHandlesLongOverlappingRuns(int size)
    {
        long[] source = Enumerable.Range(0, size).Select(i => (long)i / 2).ToArray();
        (source[1], source[^2]) = (source[^2], source[1]);
        (source[size / 4], source[3 * (size / 4)]) =
            (source[3 * (size / 4)], source[size / 4]);
        Check(source);
        Check(source.Select(x => (int)x).ToArray());
        Check(source.Select(x => (double)x).ToArray());
    }

    [Theory]
    [InlineData(128)]
    [InlineData(1024)]
    [InlineData(10000)]
    [InlineData(100003)]
    public void ConsecutiveHighOutliersAndSparseSwaps(int size)
    {
        var random = new Random(size);
        for (int pattern = 0; pattern < 6; pattern++)
        {
            long[] input = Enumerable.Range(0, size).Select(i => (long)i).ToArray();
            if (pattern < 3)
            {
                // Two to four increasing high outliers followed by a normal run.
                int width = pattern + 2;
                for (int i = 0; i < width; i++)
                    (input[19 + i], input[size - width - i - 1]) =
                        (input[size - width - i - 1], input[19 + i]);
                Array.Sort(input, 19, width);
            }
            else
            {
                for (int i = 0; i < size; i += 23 + pattern * 4)
                {
                    int j = random.Next(size);
                    (input[i], input[j]) = (input[j], input[i]);
                }
            }
            Check(input);
            Check(input.Select(x => (int)x).ToArray());
            Check(input.Select(x => (double)x).ToArray());
        }
    }

    [Fact]
    public void DoubleSpecialValuesPreserveAllBits()
    {
        double[] values = Enumerable.Range(0, 4099).Select(i => (double)i - 2000).ToArray();
        values[0] = double.PositiveInfinity;
        values[31] = BitConverter.Int64BitsToDouble(unchecked((long)0xfff8000000000012UL));
        values[64] = double.NegativeInfinity;
        values[100] = -0.0;
        values[200] = 0.0;
        values[300] = BitConverter.Int64BitsToDouble(0x7ff8000000000034L);
        var expected = values.Select(BitConverter.DoubleToInt64Bits).OrderBy(x => x).ToArray();
        Sortz.Sort(values.AsSpan());
        Assert.Equal(expected, values.Select(BitConverter.DoubleToInt64Bits).OrderBy(x => x).ToArray());
        for (int i = 1; i < values.Length; i++)
            Assert.True(values[i - 1].CompareTo(values[i]) <= 0);
    }

    [Fact]
    public void OrdinalOrderingHandlesNullsAndUnicode()
    {
        string?[] values = [null, "", "\0", "a", "a\0", "a\u0100", "\uD800", "\uFFFF"];
        Check(values, StringComparer.Ordinal);
        Array.Reverse(values);
        Check(values, StringComparer.Ordinal);
    }

    private static void Check<T>(T[] source, IComparer<T>? comparer = null)
    {
        var expected = (T[])source.Clone();
        Array.Sort(expected, comparer);
        // Exercise an offset span and verify adjacent guard elements.
        var actual = new T[source.Length + 2];
        source.CopyTo(actual, 1);
        Sortz.Sort(actual.AsSpan(1, source.Length), comparer);
        Assert.Equal(expected, actual[1..^1]);
        Assert.Equal(default(T), actual[0]);
        Assert.Equal(default(T), actual[^1]);
    }
}
