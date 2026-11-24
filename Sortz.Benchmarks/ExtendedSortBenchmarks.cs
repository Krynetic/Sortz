using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;

namespace Sortz.Benchmarks;

public abstract class TypedSortBenchmark<T>
{
    [Params(128, 1024, 10000)]
    public int Count { get; set; }

    [Params(InputPattern.Random, InputPattern.Ascending, InputPattern.Duplicates)]
    public InputPattern Pattern { get; set; }

    private T[] source = null!;
    private List<T> list = null!;
    protected virtual IComparer<T>? Comparer => null;
    protected abstract T[] Create(Random random, int count);

    [GlobalSetup]
    public void Setup()
    {
        source = Create(new Random(42), Count);
        if (Pattern == InputPattern.Ascending)
            Array.Sort(source, Comparer);
        else if (Pattern == InputPattern.Duplicates)
            for (int i = 8; i < source.Length; i++)
                source[i] = source[i & 7];
        list = new List<T>(source);
    }

    [IterationSetup]
    public void Reset() => source.AsSpan().CopyTo(CollectionsMarshal.AsSpan(list));

    [Benchmark(Baseline = true)]
    public void DefaultListSort() => list.Sort(Comparer);

    [Benchmark]
    public void SortzNextSort() => SortzNext.Sort(CollectionsMarshal.AsSpan(list), Comparer);
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class DoubleSortBenchmarks : TypedSortBenchmark<double>
{
    protected override double[] Create(Random random, int count) =>
        Enumerable.Range(0, count).Select(_ => (random.NextDouble() - 0.5) * 1e10).ToArray();
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class StringSortBenchmarks : TypedSortBenchmark<string>
{
    [Params(false, true)]
    public bool Ordinal { get; set; }

    protected override IComparer<string>? Comparer => Ordinal ? StringComparer.Ordinal : null;

    protected override string[] Create(Random random, int count) =>
        Enumerable.Range(0, count).Select(_ => random.NextInt64().ToString("X16")).ToArray();
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class ObjectSortBenchmarks : TypedSortBenchmark<SortItem>
{
    protected override SortItem[] Create(Random random, int count) =>
        Enumerable.Range(0, count).Select(i => new SortItem(random.Next(), i)).ToArray();
}

public sealed record SortItem(int Key, int Id) : IComparable<SortItem>
{
    public int CompareTo(SortItem? other) => other is null ? 1 : Key.CompareTo(other.Key);
}
