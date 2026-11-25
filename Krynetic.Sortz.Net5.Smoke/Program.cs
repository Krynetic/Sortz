using System.Reflection;
using System.Runtime.Versioning;
using Krynetic.Sortz;

if (Environment.Version.Major != 5 ||
    typeof(Sortz).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName != ".NETCoreApp,Version=v5.0")
    throw new InvalidOperationException("Expected the net5.0 runtime and library asset.");

var random = new Random(42);
var reverse = Comparer<int>.Create((a, b) => b.CompareTo(a));

foreach (int count in new[] { 0, 1, 2, 16, 17, 128, 1024, 10000 })
{
    var source = Enumerable.Range(0, count).Select(_ => random.Next(-10000, 10000)).ToArray();
    Check(source);
    Check(source, reverse);
    Check(source.OrderBy(x => x).ToArray());
    Check(source.OrderByDescending(x => x).ToArray());
}

Check(new string?[] { null, "z", "a", "", "A", null }, StringComparer.Ordinal);
Check(new double[] { double.NaN, -1, 0, double.PositiveInfinity, double.NegativeInfinity });
Span<int> stack = stackalloc int[] { 3, 1, 2 };
Sortz.Sort(stack);
if (!stack.SequenceEqual(new[] { 1, 2, 3 }))
    throw new InvalidOperationException("Stack span failed.");

Console.WriteLine(".NET 5 runtime smoke tests passed.");

static void Check<T>(T[] source, IComparer<T>? comparer = null)
{
    var expected = source.ToArray();
    Array.Sort(expected, comparer);
    var list = source.ToList();
    list.Sortz(comparer);
    var span = new T[source.Length + 2];
    source.CopyTo(span, 1);
    Sortz.Sort(span.AsSpan(1, source.Length), comparer);
    if (!expected.SequenceEqual(list) || !expected.SequenceEqual(span.Skip(1).Take(source.Length)))
        throw new InvalidOperationException("Sort result mismatch.");
}
