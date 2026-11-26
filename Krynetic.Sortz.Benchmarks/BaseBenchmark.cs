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

    [Params(InputPattern.Random)]
    public InputPattern Pattern { get; set; }

    private T[] source = null!;
    private int invocationIndex;
    protected List<T>[] Lists { get; private set; } = null!;
    protected T[][] Arrays { get; private set; } = null!;
    protected virtual IComparer<T>? Comparer => null;
    protected abstract T[] Create(Random random, int count);

    protected int NextInvocationOffset() => invocationIndex++ * SortBenchmarkConfig.BatchSize;

    [GlobalSetup]
    public void Setup()
    {
        source = Create(new Random(42), Count);
        ApplyPattern(source, Pattern, Comparer, new Random(43));
        int total = SortBenchmarkConfig.BatchSize * SortBenchmarkConfig.InvocationCount;
        Lists = new List<T>[total];
        Arrays = new T[total][];
        for (int i = 0; i < total; i++)
        {
            Lists[i] = new List<T>(source);
            Arrays[i] = new T[Count];
        }
    }

    private static void ApplyPattern(T[] values, InputPattern pattern, IComparer<T>? comparer, Random random)
    {
        if (pattern == InputPattern.Random || values.Length == 0) return;

        if (pattern == InputPattern.Constant)
        {
            Array.Fill(values, values[0]);
            return;
        }

        var order = comparer ?? Comparer<T>.Default;
        if (pattern == InputPattern.Duplicates)
        {
            T[] candidates = values.AsSpan(0, Math.Min(values.Length, 8)).ToArray();
            for (int i = 0; i < values.Length; i++) values[i] = candidates[random.Next(candidates.Length)];
            return;
        }

        Array.Sort(values, order);
        switch (pattern)
        {
            case InputPattern.Ascending:
                break;
            case InputPattern.Descending:
                Array.Reverse(values);
                break;
            case InputPattern.OrganPipe:
            {
                T[] sorted = (T[])values.Clone();
                for (int i = 0; i < values.Length; i++)
                    values[i] = sorted[i <= (values.Length - 1) / 2 ? i * 2 : (values.Length - 1 - i) * 2 + (values.Length % 2 == 0 ? 1 : 0)];
                break;
            }
            case InputPattern.NearlySorted:
                for (int i = 0; i < Math.Max(1, values.Length / 31); i++)
                {
                    int first = random.Next(values.Length);
                    int second = random.Next(values.Length);
                    (values[first], values[second]) = (values[second], values[first]);
                }
                break;
            case InputPattern.Extremes:
                T min = values[0], max = values[^1];
                for (int i = 0; i < values.Length; i++) values[i] = (i & 1) == 0 ? min : max;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null);
        }
    }

    [IterationSetup]
    public void Reset()
    {
        invocationIndex = 0;
        for (int i = 0; i < Lists.Length; i++)
        {
            source.AsSpan().CopyTo(CollectionsMarshal.AsSpan(Lists[i]));
            source.AsSpan().CopyTo(Arrays[i]);
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void DefaultListSort()
    {
        var comparer = Comparer;
        foreach (var list in Lists.AsSpan(NextInvocationOffset(), SortBenchmarkConfig.BatchSize)) list.Sort(comparer);
    }

    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void SortzSort()
    {
        var comparer = Comparer;
        foreach (var list in Lists.AsSpan(NextInvocationOffset(), SortBenchmarkConfig.BatchSize)) Sortz.Sort(CollectionsMarshal.AsSpan(list), comparer);
    }
}
