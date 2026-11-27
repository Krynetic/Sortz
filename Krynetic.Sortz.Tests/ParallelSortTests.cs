using System.Globalization;
using System.Runtime.InteropServices;
using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class ParallelSortTests
{
    [Theory]
    [InlineData(65536)]
    [InlineData(65537)]
    [InlineData(131073)]
    public void ParallelPrimitiveRuns(int count)
    {
        var random = new Random(421 + count);
        Check(Bits<int>(random, count));
        Check(Bits<uint>(random, count));
        Check(Bits<long>(random, count));
        Check(Bits<ulong>(random, count));
        Check(Bits<nint>(random, count));
        Check(Bits<nuint>(random, count));
        Check(Bits<short>(random, count));
        Check(Bits<ushort>(random, count));
        Check(Bits<char>(random, count));
        Check(Bits<Int128>(random, count));
        Check(Bits<UInt128>(random, count));
        Check(Bits<float>(random, count));
        Check(Bits<double>(random, count));
        Check(Bits<Half>(random, count));
    }

    [Theory]
    [InlineData(8192)]
    [InlineData(10001)]
    [InlineData(32771)]
    public void ParallelComparisonRuns(int count)
    {
        var random = new Random(count);
        Check(Enumerable.Range(0, count).Select(_ => new decimal(random.Next(), random.Next(),
            random.Next(), random.Next(2) == 0, (byte)random.Next(29))).ToArray());
        var values = Enumerable.Range(0, count)
            .Select(i => i % 17 == 0 ? null : new Item(random.Next(32), i)).ToArray();
        var expected = values.ToArray();
        Array.Sort(expected);
        Sortz.Sort(values.AsSpan());
        for (int i = 0; i < count; i++)
            Assert.Equal(0, Comparer<Item?>.Default.Compare(expected[i], values[i]));
        Assert.Equal(expected.Where(x => x is not null).Select(x => x!.Id).Order(),
            values.Where(x => x is not null).Select(x => x!.Id).Order());
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("tr-TR")]
    public void ParallelStringsRespectCulture(string culture)
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            string?[] keys = [null, "", "a", "A", "å", "ä", "ö", "I", "i", "ı", "İ", "e\u0301", "é", "😀"];
            var random = new Random(42);
            var input = Enumerable.Range(0, 16387)
                .Select(i => i % 19 == 0 ? null : keys[random.Next(keys.Length)] + random.Next(100)).ToArray();
            foreach (var comparer in new IComparer<string?>[] { Comparer<string?>.Default, StringComparer.Ordinal })
            {
                var expected = input.ToArray();
                var actual = input.ToArray();
                Array.Sort(expected, comparer);
                Sortz.Sort(actual.AsSpan(), comparer);
                for (int i = 0; i < actual.Length; i++)
                    Assert.Equal(0, comparer.Compare(expected[i], actual[i]));
                Assert.Equal(input.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
            }
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    [Fact]
    public void ParallelSmallDomains()
    {
        var random = new Random(91);
        Check(Bits<byte>(random, 262147));
        Check(Bits<sbyte>(random, 262149));
        bool[] flags = new bool[4194311];
        int falseCount = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            flags[i] = random.Next(2) != 0;
            if (!flags[i]) falseCount++;
        }
        Sortz.Sort(flags.AsSpan());
        Assert.False(flags.AsSpan(0, falseCount).Contains(true));
        Assert.False(flags.AsSpan(falseCount).Contains(false));
    }

    [Fact]
    public void CustomComparerRemainsSequential()
    {
        int thread = Environment.CurrentManagedThreadId;
        var comparer = Comparer<int>.Create((x, y) =>
        {
            Assert.Equal(thread, Environment.CurrentManagedThreadId);
            return y.CompareTo(x);
        });
        var values = Bits<int>(new Random(7), 100003);
        var expected = values.ToArray();
        Array.Sort(expected, comparer);
        Sortz.Sort(values.AsSpan(), comparer);
        Assert.Equal(expected, values);
    }

    [Fact]
    public void ConcurrentSlicesPreserveBoundaries()
    {
        Parallel.For(0, 4, worker =>
        {
            var values = Bits<int>(new Random(worker + 11), 131075);
            int first = values[0], last = values[^1];
            var expected = values.AsSpan(1, values.Length - 2).ToArray();
            Array.Sort(expected);
            Sortz.Sort(values.AsSpan(1, values.Length - 2));
            Assert.Equal(first, values[0]);
            Assert.Equal(last, values[^1]);
            Assert.Equal(expected, values.AsSpan(1, values.Length - 2).ToArray());
        });
    }

    private static T[] Bits<T>(Random random, int count) where T : unmanaged
    {
        var values = new T[count];
        random.NextBytes(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }

    private static void Check<T>(T[] input)
    {
        var expected = input.ToArray();
        Array.Sort(expected);
        Sortz.Sort(input.AsSpan());
        Assert.Equal(expected, input);
    }

    private sealed record Item(int Key, int Id) : IComparable<Item>
    {
        public int CompareTo(Item? other) => other is null ? 1 : Key.CompareTo(other.Key);
    }
}
