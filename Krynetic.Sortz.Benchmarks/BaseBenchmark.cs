using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;

namespace Krynetic.Sortz.Benchmarks;

public abstract class PrimitiveBitSortBenchmark<T> : TypedSortBenchmark<T> where T : unmanaged
{
    protected override T[] Create(Random random, int count)
    {
        var values = new T[count];
        random.NextBytes(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }
}

public abstract class TypedSortBenchmark<T>
{
    [Params(128, 1024, 10000)]
    public int Count { get; set; }

    private T[] source = null!;
    private int invocationIndex;
    private List<T>[] lists = null!;
    protected virtual IComparer<T>? Comparer => null;
    protected abstract T[] Create(Random random, int count);

    [GlobalSetup]
    public void Setup()
    {
        source = Create(new Random(42), Count);
        lists = new List<T>[SortBenchmarkConfig.BatchSize * SortBenchmarkConfig.InvocationCount];
        for (int i = 0; i < lists.Length; i++) lists[i] = new List<T>(source);
    }

    [IterationSetup]
    public void Reset()
    {
        invocationIndex = 0;
        foreach (var list in lists) source.AsSpan().CopyTo(CollectionsMarshal.AsSpan(list));
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void DefaultListSort()
    {
        var comparer = Comparer;
        foreach (var list in lists.AsSpan(invocationIndex++ * SortBenchmarkConfig.BatchSize, SortBenchmarkConfig.BatchSize)) list.Sort(comparer);
    }

    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void SortzSort()
    {
        var comparer = Comparer;
        foreach (var list in lists.AsSpan(invocationIndex++ * SortBenchmarkConfig.BatchSize, SortBenchmarkConfig.BatchSize)) Sortz.Sort(CollectionsMarshal.AsSpan(list), comparer);
    }
}

