using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Sortz;

internal static class SimdPartition
{
    public static void SortInt32(Span<int> values)
    {
        int[]? rented = null;
        Span<int> scratch = values.Length <= 16384
            ? stackalloc int[values.Length]
            : (rented = ArrayPool<int>.Shared.Rent(values.Length)).AsSpan(0, values.Length);
        try
        {
            PartitionInt32(values, scratch, false,
                2 * (BitOperations.Log2((uint)values.Length) + 1));
        }
        finally
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented);
        }
    }

    private static void PartitionInt32(Span<int> source, Span<int> destination, bool outputInDestination, int depth)
    {
        int count = source.Length;
        if (count <= 128 || depth == 0)
        {
            if (count <= 128)
                SimdSort.SortInt32(source);
            else
                HeapSort(source);
            if (outputInDestination)
                source.CopyTo(destination);
            return;
        }
        int a = source[0], b = source[count >> 1], c = source[^1];
        int pivot = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
        int split = DistributeInt32(source, destination, pivot);
        if (split == 0 || split == count)
        {
            // All-equal and highly skewed partitions must not recurse forever.
            bool equal = true;
            for (int i = 0; i < count; i++)
                if (source[i] != pivot) { equal = false; break; }
            if (!equal)
                HeapSort(source);
            if (outputInDestination)
                source.CopyTo(destination);
            return;
        }
        PartitionInt32(destination[..split], source[..split], !outputInDestination, depth - 1);
        PartitionInt32(destination[split..], source[split..], !outputInDestination, depth - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe int DistributeInt32(ReadOnlySpan<int> source, Span<int> destination, int pivot)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        var threshold = Vector512.Create(pivot);
        int left = 0, right = source.Length, i = 0;
        const int lanes = 16;
        fixed (int* output = destination)
        {
            for (; i <= source.Length - lanes; i += lanes)
            {
                var vector = Vector512.LoadUnsafe(ref input, (nuint)i);
                var mask = Avx512F.CompareLessThan(vector, threshold);
                int low = BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
                right -= lanes - low;
                Avx512F.CompressStore(output + left, mask, vector);
                Avx512F.CompressStore(output + right, ~mask, vector);
                left += low;
            }
            for (; i < source.Length; i++)
            {
                int value = Unsafe.Add(ref input, i);
                if (value < pivot) output[left++] = value;
                else output[--right] = value;
            }
        }
        return left;
    }

    public static void SortUInt64(Span<ulong> values)
    {
        ulong[]? rented = null;
        Span<ulong> scratch = values.Length <= 8192
            ? stackalloc ulong[values.Length]
            : (rented = ArrayPool<ulong>.Shared.Rent(values.Length)).AsSpan(0, values.Length);
        try
        {
            PartitionUInt64(values, scratch, false,
                2 * (BitOperations.Log2((uint)values.Length) + 1));
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    private static void PartitionUInt64(Span<ulong> source, Span<ulong> destination, bool outputInDestination, int depth)
    {
        int count = source.Length;
        if (count <= 128 || depth == 0)
        {
            if (count <= 128)
                SimdSort.SortUInt64(source);
            else
                HeapSort(source);
            if (outputInDestination)
                source.CopyTo(destination);
            return;
        }
        ulong a = source[0], b = source[count >> 1], c = source[^1];
        ulong pivot = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
        int split = DistributeUInt64(source, destination, pivot);
        if (split == 0 || split == count)
        {
            // All-equal and highly skewed partitions must not recurse forever.
            bool equal = true;
            for (int i = 0; i < count; i++)
                if (source[i] != pivot) { equal = false; break; }
            if (!equal)
                HeapSort(source);
            if (outputInDestination)
                source.CopyTo(destination);
            return;
        }
        PartitionUInt64(destination[..split], source[..split], !outputInDestination, depth - 1);
        PartitionUInt64(destination[split..], source[split..], !outputInDestination, depth - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static unsafe int DistributeUInt64(ReadOnlySpan<ulong> source, Span<ulong> destination, ulong pivot)
    {
        ref ulong input = ref MemoryMarshal.GetReference(source);
        var threshold = Vector512.Create(pivot);
        int left = 0, right = source.Length, i = 0;
        const int lanes = 8;
        fixed (ulong* output = destination)
        {
            for (; i <= source.Length - lanes; i += lanes)
            {
                var vector = Vector512.LoadUnsafe(ref input, (nuint)i);
                var mask = Avx512F.CompareLessThan(vector, threshold);
                int low = BitOperations.PopCount(Vector512.ExtractMostSignificantBits(mask));
                right -= lanes - low;
                Avx512F.CompressStore(output + left, mask, vector);
                Avx512F.CompressStore(output + right, ~mask, vector);
                left += low;
            }
            for (; i < source.Length; i++)
            {
                ulong value = Unsafe.Add(ref input, i);
                if (value < pivot) output[left++] = value;
                else output[--right] = value;
            }
        }
        return left;
    }

    private static void HeapSort<T>(Span<T> values) where T : unmanaged, IBinaryInteger<T>
    {
        for (int i = values.Length / 2 - 1; i >= 0; i--)
            SiftDown(values, i);
        for (int end = values.Length - 1; end > 0; end--)
        {
            (values[0], values[end]) = (values[end], values[0]);
            SiftDown(values[..end], 0);
        }
    }

    private static void SiftDown<T>(Span<T> values, int root) where T : unmanaged, IBinaryInteger<T>
    {
        T value = values[root];
        while (root < values.Length / 2)
        {
            int child = root * 2 + 1;
            if (child + 1 < values.Length && values[child + 1] > values[child])
                child++;
            if (value >= values[child])
                break;
            values[root] = values[child];
            root = child;
        }
        values[root] = value;
    }
}
