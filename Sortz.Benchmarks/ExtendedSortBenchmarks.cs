using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;

namespace Krynetic.Sortz.Benchmarks;

public abstract class TypedSortBenchmark<T>
{
    [Params(128, 1024, 10000)]
    public int Count { get; set; }

    private T[] source = null!;
    private List<T>[] lists = null!;
    protected virtual IComparer<T>? Comparer => null;
    protected abstract T[] Create(Random random, int count);

    [GlobalSetup]
    public void Setup()
    {
        source = Create(new Random(42), Count);
        lists = new List<T>[SortBenchmarkConfig.BatchSize];
        for (int i = 0; i < lists.Length; i++) lists[i] = new List<T>(source);
    }

    [IterationSetup]
    public void Reset()
    {
        foreach (var list in lists) source.AsSpan().CopyTo(CollectionsMarshal.AsSpan(list));
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void DefaultListSort()
    {
        var comparer = Comparer;
        foreach (var list in lists) list.Sort(comparer);
    }

    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void SortzNextSort()
    {
        var comparer = Comparer;
        foreach (var list in lists) Sortz.Sort(CollectionsMarshal.AsSpan(list), comparer);
    }
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
