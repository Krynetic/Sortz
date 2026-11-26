using BenchmarkDotNet.Attributes;
using Krynetic.Sortz.Tests;
using System.Runtime.InteropServices;

namespace Krynetic.Sortz.Benchmarks;

public enum InputPattern
{
    Random, Ascending,
    Descending, Duplicates,
    Constant, OrganPipe,
    NearlySorted, Extremes
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class Int32SortBenchmarks : PrimitiveBitSortBenchmark<int>
{
    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void BuiltInArraySort()
    {
        foreach (var array in Arrays.AsSpan(NextInvocationOffset(), SortBenchmarkConfig.BatchSize)) Array.Sort(array);
    }

    [Benchmark(OperationsPerInvoke = SortBenchmarkConfig.BatchSize)]
    public void BuiltInSpanSort()
    {
        foreach (var list in Lists.AsSpan(NextInvocationOffset(), SortBenchmarkConfig.BatchSize)) CollectionsMarshal.AsSpan(list).Sort();
    }
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class ByteSortBenchmarks : PrimitiveBitSortBenchmark<byte> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class SByteSortBenchmarks : PrimitiveBitSortBenchmark<sbyte> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class CharSortBenchmarks : PrimitiveBitSortBenchmark<char> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class UInt16SortBenchmarks : PrimitiveBitSortBenchmark<ushort> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class Int16SortBenchmarks : PrimitiveBitSortBenchmark<short> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class UInt32SortBenchmarks : PrimitiveBitSortBenchmark<uint> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class Int64SortBenchmarks : PrimitiveBitSortBenchmark<long> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class UInt64SortBenchmarks : PrimitiveBitSortBenchmark<ulong> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class NativeIntSortBenchmarks : PrimitiveBitSortBenchmark<nint> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class NativeUIntSortBenchmarks : PrimitiveBitSortBenchmark<nuint> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class SingleSortBenchmarks : PrimitiveBitSortBenchmark<float> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class HalfSortBenchmarks : PrimitiveBitSortBenchmark<Half> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class Int128SortBenchmarks : PrimitiveBitSortBenchmark<Int128> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class UInt128SortBenchmarks : PrimitiveBitSortBenchmark<UInt128> { }

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class BooleanSortBenchmarks : TypedSortBenchmark<bool>
{
    protected override bool[] Create(Random random, int count) =>
    [
        .. Enumerable.Range(0, count).Select(_ => random.Next(2) == 1)
    ];
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class DecimalSortBenchmarks : TypedSortBenchmark<decimal>
{
    protected override decimal[] Create(Random random, int count) =>
    [
        .. Enumerable.Range(0, count).Select(_ => new decimal(
            (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
            (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
            (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
            random.Next(2) == 0, (byte)random.Next(29)))
    ];
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class DoubleSortBenchmarks : TypedSortBenchmark<double>
{
    protected override double[] Create(Random random, int count) =>
    [
        .. Enumerable.Range(0, count).Select(_ => (random.NextDouble() - 0.5) * 1e10)
    ];
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class StringSortBenchmarks : TypedSortBenchmark<string>
{
    [Params(false, true)]
    public bool Ordinal { get; set; }

    protected override IComparer<string>? Comparer => Ordinal ? StringComparer.Ordinal : null;

    protected override string[] Create(Random random, int count) =>
    [
        .. Enumerable.Range(0, count).Select(_ => random.NextInt64().ToString("X16"))
    ];
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class ObjectSortBenchmarks : TypedSortBenchmark<SortItem>
{
    protected override SortItem[] Create(Random random, int count) =>
    [
        .. Enumerable.Range(0, count).Select(i => new SortItem(random.Next(), i))
    ];
}

public sealed record SortItem(int Key, int Id) : IComparable<SortItem>
{
    public int CompareTo(SortItem? other) => other is null ? 1 : Key.CompareTo(other.Key);
}
