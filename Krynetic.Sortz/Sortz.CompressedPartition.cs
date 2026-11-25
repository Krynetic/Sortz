using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Krynetic.Sortz;

public static partial class Sortz
{
    // Ping-pong SIMD partition: no per-level copy, no branch per element,
    // and a single scratch allocation for the entire sort.
    private static void SortCompressedPartition<T>(Span<T> values)
        where T : unmanaged, IBinaryInteger<T>
    {
        T[]? rented = null;
        Span<T> scratch = values.Length * Unsafe.SizeOf<T>() <= 8192
            ? stackalloc T[values.Length]
            : (rented = ArrayPool<T>.Shared.Rent(values.Length)).AsSpan(0, values.Length);
        try
        {
            CompressedPartition(values, scratch, true,
                2 * (BitOperations.Log2((uint)values.Length) + 1));
        }
        finally
        {
            if (rented is not null) ArrayPool<T>.Shared.Return(rented);
        }
    }

    private static void CompressedPartition<T>(Span<T> source, Span<T> target,
        bool outputToSource, int depth)
        where T : unmanaged, IBinaryInteger<T>
    {
        int n = source.Length;
        if (n <= 128 || depth == 0)
        {
            FinishCompressedPartition(source, target, outputToSource);
            return;
        }

        T a = source[n >> 2], b = source[n >> 1], c = source[n - 1 - (n >> 2)];
        if (a > b) (a, b) = (b, a);
        if (b > c) b = c;
        if (a > b) b = a;
        int split = CompressSplit(source, target, b);

        // Pathological pivots and duplicate-heavy inputs use the original
        // bounded sorting kernels instead of degenerating into recursion.
        if (split < n / 16 || n - split < n / 16)
        {
            FinishCompressedPartition(source, target, outputToSource);
            return;
        }

        CompressedPartition(target[..split], source[..split], !outputToSource, depth - 1);
        CompressedPartition(target[split..], source[split..], !outputToSource, depth - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe int CompressSplit<T>(Span<T> source, Span<T> target, T pivot)
        where T : unmanaged, IBinaryInteger<T>
    {
        int lanes = Vector512<T>.Count;
        int left = 0, right = source.Length, i = 0;
        var p = Vector512.Create(pivot);
        ref T input = ref MemoryMarshal.GetReference(source);
        fixed (T* output = target)
        {
            for (; i <= source.Length - lanes; i += lanes)
            {
                var v = Vector512.LoadUnsafe(ref input, (nuint)i);
                var mask = Vector512.LessThan(v, p);
                int lower = BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
                right -= lanes - lower;
                if (typeof(T) == typeof(int))
                {
                    Avx512F.CompressStore((int*)(output + left), mask.AsInt32(), v.AsInt32());
                    Avx512F.CompressStore((int*)(output + right), (~mask).AsInt32(), v.AsInt32());
                }
                else if (typeof(T) == typeof(ulong))
                {
                    Avx512F.CompressStore((ulong*)(output + left), mask.AsUInt64(), v.AsUInt64());
                    Avx512F.CompressStore((ulong*)(output + right), (~mask).AsUInt64(), v.AsUInt64());
                }
                else
                {
                    Avx512Vbmi2.CompressStore((ushort*)(output + left), mask.AsUInt16(), v.AsUInt16());
                    Avx512Vbmi2.CompressStore((ushort*)(output + right), (~mask).AsUInt16(), v.AsUInt16());
                }
                left += lower;
            }
            for (; i < source.Length; i++)
            {
                T value = Unsafe.Add(ref input, i);
                if (value < pivot) output[left++] = value;
                else output[--right] = value;
            }
        }
        return left;
    }

    private static void FinishCompressedPartition<T>(Span<T> source, Span<T> target, bool outputToSource)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (!outputToSource)
        {
            source.CopyTo(target);
            source = target;
        }

        if (source.Length < 2) return;
        if (typeof(T) == typeof(int))
        {
            var values = Reinterpret<T, int>(source);
            if (values.Length <= 1024) SimdSort.SortInt32(values);
            else SortInt32CompactRadix(values);
        }
        else if (typeof(T) == typeof(ulong))
        {
            var values = Reinterpret<T, ulong>(source);
            if (values.Length <= 2048) SimdSort.SortUInt64(values);
            else SortUInt64Wide(values);
        }
        else
            PrimitiveSimdSort.SortUInt16(Reinterpret<T, ushort>(source));
    }
}
