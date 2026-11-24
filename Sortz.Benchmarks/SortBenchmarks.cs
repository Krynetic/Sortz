using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Sortz.Tests;
using System.Runtime.InteropServices;

namespace Sortz.Benchmarks;

public enum InputPattern
{
    Random, Ascending, Descending, Duplicates, Constant,
    OrganPipe, NearlySorted, Extremes
}

[MemoryDiagnoser]
[SimpleJob]
public class SortBenchmarks
{
    [Params(128, 1024, 10000, 100000, 1000000)]
    public int Count { get; set; }

    [ParamsAllValues]
    public InputPattern Pattern { get; set; }

    private int[] source = null!;
    private int[] array = null!;
    private List<int> list = null!;

    [GlobalSetup]
    public void Setup()
    {
        source = SortInputs.Create(Count, (int)Pattern);
        array = new int[Count];
        list = new List<int>(source);
    }

    // BenchmarkDotNet uses one invocation per iteration with IterationSetup.
    // Restore input outside the timed workload.
    [IterationSetup]
    public void Reset()
    {
        source.AsSpan().CopyTo(array);
        source.AsSpan().CopyTo(CollectionsMarshal.AsSpan(list));
    }

    [Benchmark(Baseline = true)]
    public void DefaultListSort() => list.Sort();

    [Benchmark]
    public void BuiltInArraySort() => Array.Sort(array);

    [Benchmark]
    public void BuiltInSpanSort() => CollectionsMarshal.AsSpan(list).Sort();

    [Benchmark]
    public void SortzNextSort() => SortzNext.Sort(CollectionsMarshal.AsSpan(list));
}
