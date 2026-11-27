using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Krynetic.Sortz;

public static partial class Sortz
{
    // One parallel sort per process prevents nested and concurrent sorts from
    // multiplying worker counts. Other calls retain the existing fast paths.
    private static int parallelSortActive;
    private static readonly int ParallelSortProcessorCount = Math.Min(8, Environment.ProcessorCount);
    private static readonly ParallelOptions SortParallelOptions = new()
    {
        TaskScheduler = TaskScheduler.Default,
        MaxDegreeOfParallelism = Math.Max(1, ParallelSortProcessorCount)
    };

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySortParallelReference<T>(Span<T> values, IComparer<T> comparer)
    {
        if (ParallelSortProcessorCount < 2 || Volatile.Read(ref parallelSortActive) != 0 ||
            !HasParallelDisorder<T>(values, comparer))
            return false;

        if (Interlocked.CompareExchange(ref parallelSortActive, 1, 0) != 0)
            return false;

        try
        {
            int minimumRun = Math.Max(1024, ParallelThreshold<T>() >> 1);
            int workers = 2;
            while (workers * 2 <= ParallelSortProcessorCount &&
                   values.Length / (workers * 2) >= minimumRun)
                workers *= 2;

            SortParallelRuns(values, comparer, workers);
            return true;
        }
        finally
        {
            Volatile.Write(ref parallelSortActive, 0);
        }
    }

    private static bool HasParallelDisorder<T>(ReadOnlySpan<T> values, IComparer<T> comparer)
    {
        ref T data = ref MemoryMarshal.GetReference(values);
        for (int block = 0; block < 4; block++)
        {
            int offset = (values.Length >> 2) * block;
            int ascending = 0, descending = 0;
            for (int i = 0; i < 8; i++)
            {
                int c = comparer.Compare(Unsafe.Add(ref data, offset + i),
                    Unsafe.Add(ref data, offset + i + 1));
                ascending += c < 0 ? 1 : 0;
                descending += c > 0 ? 1 : 0;
            }
            if (ascending < 2 || descending < 2) return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParallelBoundary(int length, int index, int parts) =>
        (int)((long)length * index / parts);

    private static void SortParallelRuns<T>(Span<T> values, IComparer<T> comparer, int workers)
    {
        int length = values.Length;
        var pool = ArrayPool<T>.Shared;
        T[] first = pool.Rent(length);
        T[]? second = null;
        bool clear = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        try
        {
            second = pool.Rent(length);
            values.CopyTo(first.AsSpan(0, length));

            // Only managed arrays cross thread boundaries: caller spans can be
            // stack-backed, native-backed, or slices of movable managed memory.
            Parallel.For(0, workers, SortParallelOptions, worker =>
            {
                int start = ParallelBoundary(length, worker, workers);
                int end = ParallelBoundary(length, worker + 1, workers);
                Sort(first.AsSpan(start, end - start), comparer);
            });

            T[] source = first, target = second;
            for (int run = 1; run < workers; run *= 2)
            {
                int groupSize = run * 2;
                Parallel.For(0, workers, SortParallelOptions, worker =>
                {
                    int group = worker / groupSize * groupSize;
                    int start = ParallelBoundary(length, group, workers);
                    int middle = ParallelBoundary(length, group + run, workers);
                    int end = ParallelBoundary(length, group + groupSize, workers);
                    int outputStart = ParallelBoundary(length, worker, workers);
                    int outputEnd = ParallelBoundary(length, worker + 1, workers);

                    MergeParallelRange(source, target, start, middle, end,
                        outputStart, outputEnd, comparer);
                });
                (source, target) = (target, source);
            }

            source.AsSpan(0, length).CopyTo(values);
        }
        finally
        {
            if (clear)
            {
                first.AsSpan(0, length).Clear();
                if (second is not null) second.AsSpan(0, length).Clear();
            }
            if (second is not null) pool.Return(second);
            pool.Return(first);
        }
    }

    // Find a diagonal in the merge matrix. Equal values are consumed from the
    // left run first; this gives adjacent workers disjoint input ranges.
    private static int MergeParallelRank<T>(ref T data, int start, int middle, int end,
        int rank, IComparer<T> comparer)
    {
        int leftLength = middle - start, rightLength = end - middle;
        int lo = Math.Max(0, rank - rightLength), hi = Math.Min(rank, leftLength);
        while (lo < hi)
        {
            int left = lo + ((hi - lo) >> 1);
            int right = rank - left;
            if (right > 0 && left < leftLength &&
                comparer.Compare(Unsafe.Add(ref data, middle + right - 1),
                    Unsafe.Add(ref data, start + left)) >= 0)
                lo = left + 1;
            else
                hi = left;
        }
        return lo;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareParallelKeys<T>(T a, T b, IComparer<T> comparer)
    {
        if (typeof(T) == typeof(string) && ReferenceEquals(comparer, StringComparer.Ordinal))
            return string.CompareOrdinal(Unsafe.As<T, string?>(ref a), Unsafe.As<T, string?>(ref b));
        if (typeof(T) == typeof(int))
            return Unsafe.As<T, int>(ref a).CompareTo(Unsafe.As<T, int>(ref b));
        if (typeof(T) == typeof(long))
            return Unsafe.As<T, long>(ref a).CompareTo(Unsafe.As<T, long>(ref b));
        if (typeof(T) == typeof(uint))
            return Unsafe.As<T, uint>(ref a).CompareTo(Unsafe.As<T, uint>(ref b));
        if (typeof(T) == typeof(ulong))
            return Unsafe.As<T, ulong>(ref a).CompareTo(Unsafe.As<T, ulong>(ref b));
        if (typeof(T) == typeof(double))
            return Unsafe.As<T, double>(ref a).CompareTo(Unsafe.As<T, double>(ref b));
        if (typeof(T) == typeof(float))
            return Unsafe.As<T, float>(ref a).CompareTo(Unsafe.As<T, float>(ref b));
        return comparer.Compare(a, b);
    }

    private static void MergeParallelRange<T>(T[] source, T[] target, int start, int middle,
        int end, int outputStart, int outputEnd, IComparer<T> comparer)
    {
        ref T input = ref MemoryMarshal.GetArrayDataReference(source);
        ref T output = ref MemoryMarshal.GetArrayDataReference(target);

        if (comparer.Compare(Unsafe.Add(ref input, middle - 1),
            Unsafe.Add(ref input, middle)) <= 0)
        {
            source.AsSpan(outputStart, outputEnd - outputStart)
                .CopyTo(target.AsSpan(outputStart));
            return;
        }

        // Reverse-disjoint runs need only two bulk copies, not a merge.
        if (comparer.Compare(Unsafe.Add(ref input, end - 1),
            Unsafe.Add(ref input, start)) < 0)
        {
            int rightLength = end - middle;
            int offset = outputStart - start;
            int count = outputEnd - outputStart;
            int rightCount = Math.Min(count, Math.Max(0, rightLength - offset));
            if (rightCount != 0)
                source.AsSpan(middle + offset, rightCount)
                    .CopyTo(target.AsSpan(outputStart));
            if (rightCount != count)
                source.AsSpan(start + Math.Max(0, offset - rightLength), count - rightCount)
                    .CopyTo(target.AsSpan(outputStart + rightCount));
            return;
        }

        int rank = outputStart - start;
        int leftCount = MergeParallelRank(ref input, start, middle, end, rank, comparer);
        int left = start + leftCount, right = middle + rank - leftCount;
        int write = outputStart;
        while (write < outputEnd && left < middle && right < end)
        {
            ref T a = ref Unsafe.Add(ref input, left);
            ref T b = ref Unsafe.Add(ref input, right);
            if (CompareParallelKeys(a, b, comparer) <= 0)
            {
                Unsafe.Add(ref output, write++) = a;
                left++;
            }
            else
            {
                Unsafe.Add(ref output, write++) = b;
                right++;
            }
        }

        // CopyTo uses the runtime's vectorized memmove and GC write barriers.
        int remaining = outputEnd - write;
        if (remaining != 0)
            source.AsSpan(left < middle ? left : right, remaining)
                .CopyTo(target.AsSpan(write));
    }
}
