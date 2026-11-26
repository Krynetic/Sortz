using System.Globalization;
using Anoprsst;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
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
    public const bool IncludeCelerityAndHpc = false;
    public const int BatchSize = 8;
    public const int InvocationCount = 16;

    public ComparisonConfig()
    {
        AddFilter(new BenchmarkDotNet.Filters.SimpleFilter(b => IncludeCelerityAndHpc ||
            b.Descriptor.WorkloadMethod.Name is not ("CeleritySorting" or "HPCsharp")));
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Default)
            .WithJitTieringMode(BenchmarkDotNet.Engines.JitTieringMode.Auto)
            .WithWarmupCount(5)
            .WithIterationCount(5)
            .WithLaunchCount(1)
            .WithInvocationCount(InvocationCount)
            .WithUnrollFactor(InvocationCount));
    }
}

[MemoryDiagnoser]
[Config(typeof(ComparisonConfig))]
public abstract class ComparisonBenchmarks<T> where T : IComparable<T>
{
    [Params(128, 1024, 10000)]
    public int Count { get; set; }

    [Params(InputPattern.Random, InputPattern.Ascending, InputPattern.Descending, InputPattern.NearlySorted)]
    public InputPattern Pattern { get; set; }

    private T[] source = null!;
    private int invocationIndex;
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

        lists = new List<T>[ComparisonConfig.BatchSize * ComparisonConfig.InvocationCount];
        arrays = new T[ComparisonConfig.BatchSize * ComparisonConfig.InvocationCount][];
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
        if (ComparisonConfig.IncludeCelerityAndHpc)
        {
            CeleritySort(actual);
            if (!expected.SequenceEqual(actual))
                throw new InvalidOperationException("Celerity returned an incorrect result.");
            source.CopyTo(actual, 0);
            HpcSort(actual);
            if (!expected.SequenceEqual(actual))
                throw new InvalidOperationException("HPCsharp returned an incorrect result.");
            source.CopyTo(actual, 0);
        }
        AnoprsstSort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Anoprsst returned an incorrect result.");

        source.CopyTo(actual, 0);
        SortingNetworkSort(actual);
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("SortingNetworksMergeSort returned an incorrect result.");

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
        invocationIndex = 0;
        for (int i = 0; i < lists.Length; i++)
        {
            source.AsSpan().CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lists[i]));
            source.CopyTo(arrays[i], 0);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void DefaultListSort()
    {
        foreach (var values in lists.AsSpan(invocationIndex++ * ComparisonConfig.BatchSize, ComparisonConfig.BatchSize)) values.Sort(Comparer);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void SortzSort()
    {
        foreach (var values in lists.AsSpan(invocationIndex++ * ComparisonConfig.BatchSize, ComparisonConfig.BatchSize)) global::Krynetic.Sortz.SortzExtensions.Sortz(values, Comparer);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void YamSort()
    {
        foreach (var values in lists.AsSpan(invocationIndex++ * ComparisonConfig.BatchSize, ComparisonConfig.BatchSize)) YamSorter.Sort(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void CeleritySorting()
    {
        foreach (var values in arrays.AsSpan(invocationIndex++ * ComparisonConfig.BatchSize, ComparisonConfig.BatchSize)) CeleritySort(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void HPCsharp()
    {
        foreach (var values in arrays.AsSpan(invocationIndex++ * ComparisonConfig.BatchSize, ComparisonConfig.BatchSize)) HpcSort(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void Anoprsst()
    {
        foreach (var values in arrays.AsSpan(invocationIndex++ * ComparisonConfig.BatchSize, ComparisonConfig.BatchSize)) AnoprsstSort(values);
    }

    [Benchmark(OperationsPerInvoke = ComparisonConfig.BatchSize)]
    public void SortingNetworksMergeSort()
    {
        foreach (var values in arrays.AsSpan(invocationIndex++ * ComparisonConfig.BatchSize, ComparisonConfig.BatchSize)) SortingNetworkSort(values);
    }
}

public class Int32ComparisonBenchmarks : ComparisonBenchmarks<int>
{
    protected override int CreateValue(Random random) =>
        (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
    protected override void CeleritySort(int[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(int[] values) => values.SortRadixLsd();
    protected override void AnoprsstSort(int[] values) => values.AsSpan().WithIComparableOrder().Sort();
    protected override void SortingNetworkSort(int[] values) => SortingNetworkMerge.Sort(values, Comparer, GeneratedSortingNetworks.Sort);
}

public class Int64ComparisonBenchmarks : ComparisonBenchmarks<long>
{
    protected override long CreateValue(Random random) =>
        random.NextInt64(long.MinValue, long.MaxValue);
    protected override void CeleritySort(long[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(long[] values) => values.SortRadixLsd();
    protected override void AnoprsstSort(long[] values) => values.AsSpan().WithIComparableOrder().Sort();
    protected override void SortingNetworkSort(long[] values) => SortingNetworkMerge.Sort(values, Comparer, GeneratedSortingNetworks.Sort);
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
    protected override void SortingNetworkSort(string[] values) => SortingNetworkMerge.Sort(values, Comparer, GeneratedSortingNetworks.Sort);
}

public class DoubleComparisonBenchmarks : ComparisonBenchmarks<double>
{
    protected override double CreateValue(Random random) =>
        (random.NextDouble() - 0.5) * 2_000_000d;
    protected override void CeleritySort(double[] values) => RadixSort.Sort(values.AsSpan());
    protected override void HpcSort(double[] values) => values.SortRadixMsd();
    protected override void AnoprsstSort(double[] values) => values.AsSpan().WithIComparableOrder().Sort();
    protected override void SortingNetworkSort(double[] values) => SortingNetworkMerge.Sort(values, Comparer, GeneratedSortingNetworks.Sort);
}

internal readonly struct OrdinalStringOrdering : IOrdering<string>
{
    public bool LessThan(string a, string b) => string.CompareOrdinal(a, b) < 0;
}

[SortingNetwork(16, typeof(int))]
[SortingNetwork(32, typeof(int))]
[SortingNetwork(64, typeof(int))]
[SortingNetwork(16, typeof(long))]
[SortingNetwork(32, typeof(long))]
[SortingNetwork(64, typeof(long))]
[SortingNetwork(16, typeof(double))]
[SortingNetwork(32, typeof(double))]
[SortingNetwork(64, typeof(double))]
[SortingNetwork(16, typeof(string))]
[SortingNetwork(32, typeof(string))]
[SortingNetwork(64, typeof(string))]
internal partial class GeneratedSortingNetworks { }

internal static class SortingNetworkMerge
{
    private const int BlockSize = 64;

    internal delegate void BlockSorter<T>(Span<T> block);

    // SortingNetworks only generates small fixed-size networks. Sort each run
    // using the generated code, then merge all runs into one sorted array.
    // Scratch allocation and all merge work are included in the benchmark.
    internal static void Sort<T>(T[] values, IComparer<T> comparer, BlockSorter<T> sortBlock)
    {
        if (values.Length < 2)
            return;

        for (int offset = 0; offset < values.Length; offset += BlockSize)
        {
            Span<T> block = values.AsSpan(offset, Math.Min(BlockSize, values.Length - offset));
            // All benchmark lengths use only 64-, 32-, or 16-element runs.
            // Keep the adapter correct for other lengths as well.
            if (block.Length is 16 or 32 or BlockSize)
                sortBlock(block);
            else
                block.Sort(comparer);
        }

        if (values.Length <= BlockSize)
            return;

        Span<T> source = values;
        Span<T> destination = new T[values.Length];
        for (int width = BlockSize; width < values.Length; width *= 2)
        {
            for (int start = 0; start < values.Length; start += 2 * width)
            {
                int middle = Math.Min(start + width, values.Length);
                int end = Math.Min(start + 2 * width, values.Length);

                if (middle == end || comparer.Compare(source[middle - 1], source[middle]) <= 0)
                {
                    source[start..end].CopyTo(destination[start..end]);
                    continue;
                }

                int left = start, right = middle, index = start;
                while (left < middle && right < end)
                    destination[index++] = comparer.Compare(source[left], source[right]) <= 0
                        ? source[left++]
                        : source[right++];

                source[left..middle].CopyTo(destination[index..]);
                index += middle - left;
                source[right..end].CopyTo(destination[index..]);
            }

            Span<T> temporary = source;
            source = destination;
            destination = temporary;
        }

        source.CopyTo(values);
    }
}
