using BenchmarkDotNet.Attributes;
using Krynetic.Sortz.Tests;
using System.Runtime.InteropServices;

namespace Krynetic.Sortz.Benchmarks;

public enum InputPattern
{
    Random, Ascending, Descending, Duplicates, Constant,
    OrganPipe, NearlySorted, Extremes
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class SortBenchmarks
{
    [Params(128, 1024, 10000)] //, 100000, 1000000)]
    public int Count { get; set; }

    private int[] source = null!;
    private int[][] arrays = null!;
    private List<int>[] lists = null!;

    [GlobalSetup]
    public void Setup()
    {
        source = SortInputs.Create(Count, (int)InputPattern.Random);
        arrays = new int[SortBenchmarkConfig.BatchSize][];
        lists = new List<int>[SortBenchmarkConfig.BatchSize];
        for (int i = 0; i < arrays.Length; i++)
        {
            arrays[i] = new int[Count];
            lists[i] = new List<int>(source);
        }
    }

    // Each invocation sorts independent buffers restored outside the timed workload.
    // OperationsPerInvoke reports mean time per sort, not per batch.
    [IterationSetup]
    public void Reset()
    {
        for (int i = 0; i < arrays.Length; i++)
        {
            source.AsSpan().CopyTo(arrays[i]);
            source.AsSpan().CopyTo(CollectionsMarshal.AsSpan(lists[i]));
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void DefaultListSort()
    {
        foreach (var list in lists) list.Sort();
    }

    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void BuiltInArraySort()
    {
        foreach (var array in arrays) Array.Sort(array);
    }

    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void BuiltInSpanSort()
    {
        foreach (var list in lists) CollectionsMarshal.AsSpan(list).Sort();
    }

    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void SortzNextSort()
    {
        foreach (var list in lists) Sortz.Sort(CollectionsMarshal.AsSpan(list));
    }
}
