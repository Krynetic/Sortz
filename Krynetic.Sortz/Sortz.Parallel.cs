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

    // Initial crossover estimates from the existing 10K random-input timings.
    // Small-domain counters and SIMD/radix paths need substantially more work
    // before scheduling and copying can be amortized.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetParallelThreshold<T>()
    {
        if (typeof(T) == typeof(bool)) return 4 * 1024 * 1024;
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte)) return 256 * 1024;
        if (typeof(T) == typeof(short) || typeof(T) == typeof(ushort) ||
            typeof(T) == typeof(char) || typeof(T) == typeof(Half)) return 128 * 1024;
        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
            typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
            typeof(T) == typeof(nint) || typeof(T) == typeof(nuint) ||
            typeof(T) == typeof(float) || typeof(T) == typeof(double) ||
            typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128)) return 64 * 1024;
        if (typeof(T) == typeof(decimal)) return 64 * 1024;
        if (typeof(T) == typeof(string)) return 128 * 1024;
        if (!typeof(T).IsValueType) return 8192;
        return 16 * 1024;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TrySortParallel<T>(Span<T> values, IComparer<T>? comparer)
    {
        if (ParallelSortProcessorCount < 2 || Volatile.Read(ref parallelSortActive) != 0)
            return false;

        bool ordinal = typeof(T) == typeof(string) && ReferenceEquals(comparer, StringComparer.Ordinal);
        // Do not call a caller-supplied, possibly stateful comparer concurrently.
        if (!ordinal && comparer is not null && !ReferenceEquals(comparer, Comparer<T>.Default))
            return false;

        comparer ??= Comparer<T>.Default;
        // Reject ordered inputs before acquiring the global gate.
        if (typeof(T) != typeof(bool) && !HasParallelDisorder<T>(values, comparer))
            return false;
        if (Interlocked.CompareExchange(ref parallelSortActive, 1, 0) != 0)
            return false;

        try
        {

            int length = values.Length;
            int grain = GetParallelThreshold<T>() / 2;
            if (!typeof(T).IsValueType && typeof(T) != typeof(string))
                grain = 1024;

            int workers = 2;
            while (workers * 2 <= ParallelSortProcessorCount && length / (workers * 2) >= grain)
                workers *= 2;

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
                SortParallelSmallDomain(values, workers);
            else if (IsParallelRadixInteger<T>())
                SortParallelRadixBuckets(values, workers);
            else
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsParallelRadixInteger<T>() =>
        typeof(T) == typeof(short) || typeof(T) == typeof(ushort) || typeof(T) == typeof(char) ||
        typeof(T) == typeof(int) || typeof(T) == typeof(uint) ||
        typeof(T) == typeof(long) || typeof(T) == typeof(ulong) ||
        typeof(T) == typeof(nint) || typeof(T) == typeof(nuint) ||
        typeof(T) == typeof(Int128) || typeof(T) == typeof(UInt128);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ParallelRadixKey<T>(T value)
    {
        if (typeof(T) == typeof(short)) return unchecked((ushort)(Unsafe.As<T, short>(ref value) ^ short.MinValue));
        if (typeof(T) == typeof(ushort) || typeof(T) == typeof(char)) return Unsafe.As<T, ushort>(ref value);
        if (typeof(T) == typeof(int)) return unchecked((uint)(Unsafe.As<T, int>(ref value) ^ int.MinValue));
        if (typeof(T) == typeof(uint)) return Unsafe.As<T, uint>(ref value);
        if (typeof(T) == typeof(long)) return unchecked((ulong)(Unsafe.As<T, long>(ref value) ^ long.MinValue));
        if (typeof(T) == typeof(ulong)) return Unsafe.As<T, ulong>(ref value);
        if (typeof(T) == typeof(nint)) return unchecked((nuint)(Unsafe.As<T, nint>(ref value) ^ nint.MinValue));
        if (typeof(T) == typeof(nuint)) return Unsafe.As<T, nuint>(ref value);
        if (typeof(T) == typeof(Int128)) return (ulong)(unchecked((UInt128)(Unsafe.As<T, Int128>(ref value) ^ Int128.MinValue)) >> 64);
        return (ulong)(Unsafe.As<T, UInt128>(ref value) >> 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ParallelRadixBucket(ulong key, ulong minimum, int shift)
    {
        ulong relative = key >= minimum ? key - minimum : 0;
        return (int)Math.Min(31UL, relative >> shift);
    }

    private static void SortParallelRadixBuckets<T>(Span<T> values, int workers)
    {
        const int buckets = 32;
        const int stride = 64;
        int length = values.Length;
        var pool = ArrayPool<T>.Shared;
        T[] source = pool.Rent(length);
        T[]? target = null;
        int[] counts = new int[workers * stride];
        int[] offsets = new int[buckets + 1];
        try
        {
            // Sampling establishes a monotonic bucket mapping. Outliers are
            // clamped into the edge buckets, so sampling cannot affect correctness.
            ulong minimum = ulong.MaxValue, maximum = 0;
            const int sampleCount = 128;
            for (int i = 0; i < sampleCount; i++)
            {
                ulong key = ParallelRadixKey(values[ParallelBoundary(length, i, sampleCount)]);
                minimum = Math.Min(minimum, key);
                maximum = Math.Max(maximum, key);
            }
            ulong range = maximum - minimum;
            if (range == 0)
            {
                SortParallelRuns(values, Comparer<T>.Default, workers);
                return;
            }
            int shift = Math.Max(0, 64 - System.Numerics.BitOperations.LeadingZeroCount(range) - 5);
            values.CopyTo(source.AsSpan(0, length));
            Parallel.For(0, workers, SortParallelOptions, worker =>
            {
                int start = ParallelBoundary(length, worker, workers);
                int end = ParallelBoundary(length, worker + 1, workers);
                int row = worker * stride;
                for (int i = start; i < end; i++)
                    counts[row + ParallelRadixBucket(ParallelRadixKey(source[i]), minimum, shift)]++;
            });
            int total = 0, largest = 0;
            for (int bucket = 0; bucket < buckets; bucket++)
            {
                offsets[bucket] = total;
                for (int worker = 0; worker < workers; worker++)
                {
                    int index = worker * stride + bucket;
                    int count = counts[index];
                    counts[index] = total;
                    total += count;
                }
                largest = Math.Max(largest, total - offsets[bucket]);
            }
            offsets[buckets] = total;
            if (largest > length / 2)
            {
                // Release the distribution scratch before the merge fallback.
                pool.Return(source);
                source = null!;
                SortParallelRuns(values, Comparer<T>.Default, workers);
                return;
            }

            T[] output = target = pool.Rent(length);
            Parallel.For(0, workers, SortParallelOptions, worker =>
            {
                int start = ParallelBoundary(length, worker, workers);
                int end = ParallelBoundary(length, worker + 1, workers);
                int row = worker * stride;
                for (int i = start; i < end; i++)
                {
                    T value = source[i];
                    int bucket = ParallelRadixBucket(ParallelRadixKey(value), minimum, shift);
                    output[counts[row + bucket]++] = value;
                }
            });
            Parallel.For(0, buckets, SortParallelOptions, bucket =>
            {
                int start = offsets[bucket], count = offsets[bucket + 1] - start;
                if (count > 1) Sort(output.AsSpan(start, count));
            });
            output.AsSpan(0, length).CopyTo(values);
        }
        finally
        {
            if (target is not null) pool.Return(target);
            if (source is not null) pool.Return(source);
        }
    }

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

        int rank = outputStart - start;
        int leftCount = MergeParallelRank(ref input, start, middle, end, rank, comparer);
        int left = start + leftCount, right = middle + rank - leftCount;
        int finalRank = outputEnd - start;
        int finalLeft = MergeParallelRank(ref input, start, middle, end, finalRank, comparer);
        int leftEnd = start + finalLeft, rightEnd = middle + finalRank - finalLeft;
        int write = outputStart;
        while (left < leftEnd && right < rightEnd)
        {
            ref T a = ref Unsafe.Add(ref input, left);
            ref T b = ref Unsafe.Add(ref input, right);
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                bool takeLeft = comparer.Compare(a, b) <= 0;
                Unsafe.Add(ref output, write++) = takeLeft ? a : b;
                left += takeLeft ? 1 : 0;
                right += takeLeft ? 0 : 1;
            }
            else if (comparer.Compare(a, b) <= 0)
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
            source.AsSpan(left < leftEnd ? left : right, remaining)
                .CopyTo(target.AsSpan(write));
    }

    private static void SortParallelSmallDomain<T>(Span<T> values, int workers)
    {
        int length = values.Length;
        T[] copy = ArrayPool<T>.Shared.Rent(length);
        try
        {
            values.CopyTo(copy.AsSpan(0, length));
            if (typeof(T) == typeof(bool))
            {
                const int counterStride = 16;
                int[] totals = ArrayPool<int>.Shared.Rent(workers * counterStride);
                try
                {
                    Parallel.For(0, workers, SortParallelOptions, worker =>
                    {
                        int start = ParallelBoundary(length, worker, workers);
                        int end = ParallelBoundary(length, worker + 1, workers);
                        totals[worker * counterStride] = CountFalse(Reinterpret<T, bool>(copy.AsSpan(start, end - start)));
                    });
                    int falseCount = 0;
                    for (int i = 0; i < workers; i++) falseCount += totals[i * counterStride];
                    var flags = Reinterpret<T, bool>(values);
                    flags[..falseCount].Clear();
                    flags[falseCount..].Fill(true);
                }
                finally { ArrayPool<int>.Shared.Return(totals); }
                return;
            }

            const int stride = 1024;
            int[] histograms = ArrayPool<int>.Shared.Rent(workers * stride);
            int sign = typeof(T) == typeof(sbyte) ? 128 : 0;
            try
            {
                Parallel.For(0, workers, SortParallelOptions, worker =>
                {
                    int start = ParallelBoundary(length, worker, workers);
                    int end = ParallelBoundary(length, worker + 1, workers);
                    CountParallelBytes(Reinterpret<T, byte>(copy.AsSpan(start, end - start)),
                        histograms.AsSpan(worker * stride, stride), sign);
                });

                var bytes = Reinterpret<T, byte>(values);
                int offset = 0;
                for (int key = 0; key < 256; key++)
                {
                    int count = 0;
                    for (int worker = 0; worker < workers; worker++)
                    {
                        int index = worker * stride + key;
                        count += histograms[index] + histograms[index + 256] +
                            histograms[index + 512] + histograms[index + 768];
                    }
                    bytes.Slice(offset, count).Fill((byte)(key ^ sign));
                    offset += count;
                }
            }
            finally { ArrayPool<int>.Shared.Return(histograms); }
        }
        finally { ArrayPool<T>.Shared.Return(copy); }
    }

    private static void CountParallelBytes(Span<byte> values, Span<int> counts, int sign)
    {
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        ref byte data = ref MemoryMarshal.GetReference(values);
        ulong mask = 0x0101010101010101UL * (uint)sign;
        int i = 0;
        for (; i <= values.Length - 8; i += 8)
        {
            ulong word = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref data, i)) ^ mask;
            Unsafe.Add(ref h, (byte)word)++;
            Unsafe.Add(ref h, 256 + (byte)(word >> 8))++;
            Unsafe.Add(ref h, 512 + (byte)(word >> 16))++;
            Unsafe.Add(ref h, 768 + (byte)(word >> 24))++;
            Unsafe.Add(ref h, (byte)(word >> 32))++;
            Unsafe.Add(ref h, 256 + (byte)(word >> 40))++;
            Unsafe.Add(ref h, 512 + (byte)(word >> 48))++;
            Unsafe.Add(ref h, 768 + (byte)(word >> 56))++;
        }
        for (; i < values.Length; i++)
            Unsafe.Add(ref h, Unsafe.Add(ref data, i) ^ sign)++;
    }
}
