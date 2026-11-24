using BenchmarkDotNet.Attributes;
using System.Runtime.InteropServices;

namespace Sortz.Benchmarks;

public abstract class PrimitiveBitSortBenchmark<T> : TypedSortBenchmark<T> where T : unmanaged
{
    protected override T[] Create(Random random, int count)
    {
        var values = new T[count];
        random.NextBytes(MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
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
        Enumerable.Range(0, count).Select(_ => random.Next(2) == 1).ToArray();
}

[MemoryDiagnoser]
[Config(typeof(SortBenchmarkConfig))]
public class DecimalSortBenchmarks : TypedSortBenchmark<decimal>
{
    protected override decimal[] Create(Random random, int count) =>
        Enumerable.Range(0, count).Select(_ => new decimal(
            (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
            (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
            (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
            random.Next(2) == 0, (byte)random.Next(29))).ToArray();
}
