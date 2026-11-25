using System.Globalization;
using Anoprsst;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using Celerity.Sorting;
using HPCsharp;
using SortingNetworks;
using YamSort;

namespace Krynetic.Sortz.Comparison;

public enum InputPattern
{
    Random, Ascending, Descending, NearlySorted
}

public sealed class ComparisonConfig : ManualConfig
{
    public const int BatchSize = 16;

    public static bool HasSortingNetwork(int count) => count is 27 or 28;

    public ComparisonConfig()
    {
        // Generated sorting networks only support the exact lengths declared below.
        AddFilter(new SimpleFilter(benchmark =>
            benchmark.Descriptor.WorkloadMethod.Name != nameof(ComparisonBenchmarks<int>.SortingNetworks) ||
            benchmark.Parameters.Items.Any(parameter =>
                parameter.Name == nameof(ComparisonBenchmarks<int>.Count) &&
                parameter.Value is int count && HasSortingNetwork(count))));

        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Default)
            .WithWarmupCount(20)
            .WithIterationCount(40)
            .WithLaunchCount(1)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));
    }
}

[MemoryDiagnoser]
[Config(typeof(ComparisonConfig))]
public abstract class ComparisonBenchmarks<T> where T : IComparable<T>
{
    [Params(27, 28, 128, 1024, 10000, 100000)]
    public int Count { get; set; }

    [Params(InputPattern.Random, InputPattern.Ascending, InputPattern.Descending, InputPattern.NearlySorted)]
    public InputPattern Pattern { get; set; }

    private T[] source = null!;
    private List<T>[] lists = null!;
    private T[][] arrays = null!;

    protected abstract T CreateValue(Random random);
    protected virtual IComparer<T> Comparer => System.Collections.Generic.Comparer<T>.Default;
    protected abstract void CeleritySort(T[] values);
    protected abstract void HpcSort(T[] values);
    protected abstract void AnoprsstSort(T[] values);
    protected abstract void SortingNetworkSort(T[] values);

    [GlobalSetup]
    public void Setup()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var random = new Random(42);
        source = new T[Count];
        for (int i = 0; i < Count; i++)
            source[i] = CreateValue(random);

        if (Pattern != InputPattern.Random)
        {
            Array.Sort(source, Comparer);
            if (Pattern == InputPattern.Descending)
                Array.Reverse(source);
            else if (Pattern == InputPattern.NearlySorted)
            {
                for (int i = 0; i < source.Length; i += 31)
                {
                    int j = random.Next(source.Length);
                    (source[i], source[j]) = (source[j], source[i]);
                }
            }
        }

        lists = new List<T>[ComparisonConfig.BatchSize];
        arrays = new T[ComparisonConfig.BatchSize][];
        for (int i = 0; i < lists.Length; i++)
        {
            lists[i] = new List<T>(source);
            arrays[i] = new T[Count];
        }

        var expected = (T[])source.Clone();
        Array.Sort(expected, Comparer);
        Validate(expected, values => values.Sort(Comparer));
        Validate(expected, values => global::Krynetic.Sortz.SortzExtensions.Sortz(values, Comparer));
        Validate(expected, values => YamSorter.Sort(values));

        var actual = (T[])source.Clone();
        CeleritySort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Celerity returned an incorrect result.");
        source.CopyTo(actual, 0);
        HpcSort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("HPCsharp returned an incorrect result.");
        source.CopyTo(actual, 0);
        AnoprsstSort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Anoprsst returned an incorrect result.");

        if (ComparisonConfig.HasSortingNetwork(Count))
        {
            source.CopyTo(actual, 0);
            SortingNetworkSort(actual);
            if (!expected.SequenceEqual(actual))
                throw new InvalidOperationException("SortingNetworks returned an incorrect result.");
        }

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
        foreach (var values in lists) values.Sort(Comparer);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void SortzSort()
    {
        foreach (var values in lists) global::Krynetic.Sortz.SortzExtensions.Sortz(values, Comparer);
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

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void Anoprsst()
    {
        foreach (var values in arrays) AnoprsstSort(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void SortingNetworks()
    {
        foreach (var values in arrays) SortingNetworkSort(values);
    }
}

public class Int32ComparisonBenchmarks : ComparisonBenchmarks<int>
{
    protected override int CreateValue(Random random) =>
        (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
    protected override void CeleritySort(int[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(int[] values) => values.SortRadixLsd();
    protected override void AnoprsstSort(int[] values) => values.AsSpan().WithIComparableOrder().Sort();
    protected override void SortingNetworkSort(int[] values) => GeneratedSortingNetworks.Sort(values.AsSpan());
}

public class Int64ComparisonBenchmarks : ComparisonBenchmarks<long>
{
    protected override long CreateValue(Random random) =>
        random.NextInt64(long.MinValue, long.MaxValue);
    protected override void CeleritySort(long[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(long[] values) => values.SortRadixLsd();
    protected override void AnoprsstSort(long[] values) => values.AsSpan().WithIComparableOrder().Sort();
    protected override void SortingNetworkSort(long[] values) => GeneratedSortingNetworks.Sort(values.AsSpan());
}

public class StringComparisonBenchmarks : ComparisonBenchmarks<string>
{
    protected override IComparer<string> Comparer => StringComparer.Ordinal;

    protected override string CreateValue(Random random) =>
        string.Create(random.Next(8, 25), random, static (span, rng) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = (char)('a' + rng.Next(26));
        });

    protected override void CeleritySort(string[] values) => StringSort.Sort(values.AsSpan());
    protected override void HpcSort(string[] values) => values.SortMergeInPlace(comparer: StringComparer.Ordinal);
    protected override void AnoprsstSort(string[] values) => values.AsSpan().WithOrder(new OrdinalStringOrdering()).Sort();
    protected override void SortingNetworkSort(string[] values) => GeneratedSortingNetworks.Sort(values.AsSpan(), StringComparer.Ordinal);
}

public class DoubleComparisonBenchmarks : ComparisonBenchmarks<double>
{
    protected override double CreateValue(Random random) =>
        (random.NextDouble() - 0.5) * 2_000_000d;
    protected override void CeleritySort(double[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(double[] values) => values.SortRadixMsd();
    protected override void AnoprsstSort(double[] values) => values.AsSpan().WithIComparableOrder().Sort();
    protected override void SortingNetworkSort(double[] values) => GeneratedSortingNetworks.Sort(values.AsSpan());
}

internal readonly struct OrdinalStringOrdering : IOrdering<string>
{
    public bool LessThan(string a, string b) => string.CompareOrdinal(a, b) < 0;
}

[SortingNetwork(27, typeof(int))]
[SortingNetwork(28, typeof(int))]
[SortingNetwork(27, typeof(long))]
[SortingNetwork(28, typeof(long))]
[SortingNetwork(27, typeof(double))]
[SortingNetwork(28, typeof(double))]
[SortingNetwork(27, typeof(string))]
[SortingNetwork(28, typeof(string))]
internal partial class GeneratedSortingNetworks { }
