using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Celerity.Sorting;
using HPCsharp;
using YamSort;

namespace Krynetic.Sortz.Comparison;

public sealed class ComparisonConfig : ManualConfig
{
    public const int BatchSize = 16;

    public ComparisonConfig()
    {
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Default)
            .WithWarmupCount(20)
            .WithIterationCount(40)
            .WithLaunchCount(1)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));
    }
}

public enum Distribution { Random, Ascending, Descending, Duplicates }

[MemoryDiagnoser]
[Config(typeof(ComparisonConfig))]
public abstract class ComparisonBenchmarks<T> where T : IComparable<T>
{
    [Params(128, 1024, 10000, 100000)]
    public int Count { get; set; }

    [Params(Distribution.Random, Distribution.Ascending, Distribution.Descending, Distribution.Duplicates)]
    public Distribution Pattern { get; set; }

    private T[] source = null!;
    private List<T>[] lists = null!;
    private T[][] arrays = null!;

    protected abstract T CreateValue(Random random, bool duplicates);
    protected abstract void CeleritySort(T[] values);
    protected abstract void HpcSort(T[] values);

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        source = new T[Count];
        for (int i = 0; i < Count; i++)
            source[i] = CreateValue(random, Pattern == Distribution.Duplicates);
        if (Pattern is Distribution.Ascending or Distribution.Descending)
        {
            Array.Sort(source);
            if (Pattern == Distribution.Descending)
                Array.Reverse(source);
        }

        lists = new List<T>[ComparisonConfig.BatchSize];
        arrays = new T[ComparisonConfig.BatchSize][];
        for (int i = 0; i < lists.Length; i++)
        {
            lists[i] = new List<T>(source);
            arrays[i] = new T[Count];
        }

        var expected = (T[])source.Clone();
        Array.Sort(expected);
        Validate(expected, values => values.Sort());
        Validate(expected, values => global::Krynetic.Sortz.SortzExtensions.Sortz(values));
        Validate(expected, values => YamSorter.Sort(values));

        var actual = (T[])source.Clone();
        CeleritySort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Celerity returned an incorrect result.");
        source.CopyTo(actual, 0);
        HpcSort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("HPCsharp returned an incorrect result.");
        Reset();
    }

    private void Validate(T[] expected, Action<List<T>> sort)
    {
        var actual = new List<T>(source);
        sort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("List sorter returned an incorrect result.");
    }

    [IterationSetup]
    public void Reset()
    {
        for (int i = 0; i < lists.Length; i++)
        {
            source.AsSpan().CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lists[i]));
            source.CopyTo(arrays[i], 0);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void DefaultListSort()
    {
        foreach (var values in lists) values.Sort();
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void SortzSort()
    {
        foreach (var values in lists) global::Krynetic.Sortz.SortzExtensions.Sortz(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void YamSort()
    {
        foreach (var values in lists) YamSorter.Sort(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void CeleritySorting()
    {
        foreach (var values in arrays) CeleritySort(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void HPCsharp()
    {
        foreach (var values in arrays) HpcSort(values);
    }
}

public class Int32ComparisonBenchmarks : ComparisonBenchmarks<int>
{
    protected override int CreateValue(Random random, bool duplicates) =>
        duplicates ? random.Next(-8, 8) : (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
    protected override void CeleritySort(int[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(int[] values) => values.SortRadixLsd();
}

public class Int64ComparisonBenchmarks : ComparisonBenchmarks<long>
{
    protected override long CreateValue(Random random, bool duplicates) =>
        duplicates ? random.Next(-8, 8) : random.NextInt64(long.MinValue, long.MaxValue);
    protected override void CeleritySort(long[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(long[] values) => values.SortRadixLsd();
}

public class SingleComparisonBenchmarks : ComparisonBenchmarks<float>
{
    protected override float CreateValue(Random random, bool duplicates) =>
        duplicates ? random.Next(-8, 8) : (random.NextSingle() - 0.5f) * 2_000_000f;
    protected override void CeleritySort(float[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(float[] values) => values.SortRadixMsd();
}

public class DoubleComparisonBenchmarks : ComparisonBenchmarks<double>
{
    protected override double CreateValue(Random random, bool duplicates) =>
        duplicates ? random.Next(-8, 8) : (random.NextDouble() - 0.5) * 2_000_000d;
    protected override void CeleritySort(double[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(double[] values) => values.SortRadixMsd();
}
