using System.Globalization;
using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class ExtendedSortTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(8193)]
    [InlineData(100000)]
    public void DoubleBitPatterns(int count)
    {
        var random = new Random(42);
        var bytes = new byte[count * sizeof(double)];
        random.NextBytes(bytes);
        var input = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, double>(bytes.AsSpan()).ToArray();
        Check(input);
        var actual = (double[])input.Clone();
        Sorter.Sort(actual.AsSpan());
        // Even NaN payloads and signed zero representations must be preserved.
        Assert.Equal(input.Select(BitConverter.DoubleToUInt64Bits).Order(),
            actual.Select(BitConverter.DoubleToUInt64Bits).Order());
    }

    [Theory]
    [InlineData(65)]
    [InlineData(10000)]
    public void DoubleSpecialValues(int count)
    {
        double[] special = [double.NaN, double.NegativeInfinity, double.PositiveInfinity,
            -0.0, 0.0, double.Epsilon, -double.Epsilon, double.MaxValue, double.MinValue,
            BitConverter.UInt64BitsToDouble(0x7ff8000000000123UL), -1, 1];
        var random = new Random(47);
        var data = Enumerable.Range(0, count).Select(_ => special[random.Next(special.Length)]).ToArray();
        Check(data);
        Check(data, Comparer<double>.Create((a, b) => b.CompareTo(a)));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    public void StringsRespectCulture(string name)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            string?[] corpus = [null, "", "a", "A", "ä", "å", "ö", "I", "i", "ı", "İ",
                "é", "e\u0301", "a\0b", "ab", "\ud800", "\udfff", "😀"];
            var random = new Random(42);
            var data = Enumerable.Range(0, 10000).Select(_ => corpus[random.Next(corpus.Length)]).ToArray();
            Check(data);
            Check(data, StringComparer.Ordinal);
            Check(data, StringComparer.OrdinalIgnoreCase);
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(80)]
    [InlineData(500)]
    public void OrdinalSharedPrefixes(int length)
    {
        string prefix = new('x', length);
        var random = new Random(18);
        var data = Enumerable.Range(0, 10000)
            .Select(i => i % 11 == 0 ? null : prefix + (char)random.Next(65536) + i % 17)
            .ToArray();
        Check(data, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(10000)]
    public void ComparableObjects(int count)
    {
        var random = new Random(12);
        var data = Enumerable.Range(0, count)
            .Select(i => i % 11 == 0 ? null : new Item(random.Next(32), i)).ToArray();
        Check(data);
        Check(data, Comparer<Item?>.Create((a, b) => Comparer<Item?>.Default.Compare(b, a)));
    }

    private static void Check<T>(T[] input, IComparer<T>? comparer = null)
    {
        comparer ??= Comparer<T>.Default;
        var expected = input.ToList();
        var actual = input.ToList();
        expected.Sort(comparer);
        actual.SortNext(comparer);
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < actual.Count; i++)
            Assert.Equal(0, comparer.Compare(expected[i], actual[i]));
        // Equal-comparing elements need not have the same relative order.
        var output = actual.ToLookup(x => x);
        Assert.True(input.ToLookup(x => x).All(g => g.Count() == output[g.Key].Count()));
    }

    private sealed record Item(int Key, int Id) : IComparable<Item>
    {
        public int CompareTo(Item? other) => other is null ? 1 : Key.CompareTo(other.Key);
    }
}
