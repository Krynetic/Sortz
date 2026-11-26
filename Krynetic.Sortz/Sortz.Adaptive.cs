using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Krynetic.Sortz;

public static partial class Sortz
{
    // The endpoints establish the only possible monotonic direction.
    // Overlapping vector loads check every adjacent pair, including boundaries.
    // Floating point NaNs fail the vector check and use the exact scalar ordering
    // in the adaptive or general sorter instead.
    private static bool TryOrderedNumeric<T>(Span<T> values)
        where T : unmanaged, IComparable<T>
    {
        ref T data = ref MemoryMarshal.GetReference(values);
        int length = values.Length;
        if (length < 2) return true;

        bool descending = data.CompareTo(Unsafe.Add(ref data, length - 1)) > 0;
        int i = 1;
        // Reject disorder quickly without setting up vectors on random inputs.
        for (; i < Math.Min(length, 5); i++)
        {
            int c = Unsafe.Add(ref data, i - 1).CompareTo(Unsafe.Add(ref data, i));
            if (descending ? c < 0 : c > 0) return false;
        }

        if (Vector512.IsHardwareAccelerated && Vector512<T>.IsSupported)
        {
            int width = Vector512<T>.Count;
            for (; i <= length - width; i += width)
            {
                var a = Vector512.LoadUnsafe(ref data, (nuint)(i - 1));
                var b = Vector512.LoadUnsafe(ref data, (nuint)i);
                if (!(descending ? Vector512.GreaterThanOrEqualAll(a, b) :
                    Vector512.LessThanOrEqualAll(a, b))) return false;
            }
        }
        else if (Vector256.IsHardwareAccelerated && Vector256<T>.IsSupported)
        {
            int width = Vector256<T>.Count;
            for (; i <= length - width; i += width)
            {
                var a = Vector256.LoadUnsafe(ref data, (nuint)(i - 1));
                var b = Vector256.LoadUnsafe(ref data, (nuint)i);
                if (!(descending ? Vector256.GreaterThanOrEqualAll(a, b) :
                    Vector256.LessThanOrEqualAll(a, b))) return false;
            }
        }

        for (; i < length; i++)
        {
            int c = Unsafe.Add(ref data, i - 1).CompareTo(Unsafe.Add(ref data, i));
            if (descending ? c < 0 : c > 0) return false;
        }

        if (descending) values.Reverse();
        return true;
    }

    // Ordinal strings do not need an interface comparer call per adjacent pair.
    private static bool TryOrderedOrdinal(Span<string?> values)
    {
        ref string? data = ref MemoryMarshal.GetReference(values);
        int length = values.Length;
        bool descending = string.CompareOrdinal(data, Unsafe.Add(ref data, length - 1)) > 0;
        for (int i = 1; i < length; i++)
        {
            int c = string.CompareOrdinal(Unsafe.Add(ref data, i - 1), Unsafe.Add(ref data, i));
            if (descending ? c < 0 : c > 0) return false;
        }
        if (descending) values.Reverse();
        return true;
    }

    // Keep a monotonic subsequence and sort only the discarded elements.
    // Limit scratch to one eighth of the input; if the estimate was wrong,
    // reconstruct the permutation and let the existing radix path finish.
    private static bool TrySparseRepair<T>(Span<T> values)
        where T : unmanaged, IComparable<T>
    {
        int length = values.Length;
        int inversions = 0;
        ref T data = ref MemoryMarshal.GetReference(values);
        // The numeric callers have 32- or 64-bit lanes, so sixteen
        // adjacent pairs fit exactly in one or two vectors.
        if (Vector512.IsHardwareAccelerated && Vector512<T>.IsSupported)
        {
            int width = Vector512<T>.Count;
            for (int i = 1; i <= 16; i += width)
            {
                var a = Vector512.LoadUnsafe(ref data, (nuint)(i - 1));
                var b = Vector512.LoadUnsafe(ref data, (nuint)i);
                ulong ordered = Vector512.ExtractMostSignificantBits(Vector512.LessThanOrEqual(a, b));
                inversions += width - BitOperations.PopCount(ordered);
                if (inversions > 4) return false;
            }
        }
        else
        {
            for (int i = 1; i <= 16; i++)
                if (Unsafe.Add(ref data, i - 1).CompareTo(Unsafe.Add(ref data, i)) > 0 &&
                    ++inversions > 4)
                    return false;
        }

        // Reject locally ordered prefixes followed by globally random data.
        if (length >= 256)
        {
            int stride = length >> 2;
            for (int block = 1; block <= 3; block++)
            {
                int start = stride * block;
                inversions = 0;
                for (int i = start; i < start + 8; i++)
                    if (Unsafe.Add(ref data, i - 1).CompareTo(Unsafe.Add(ref data, i)) > 0 &&
                        ++inversions > 3)
                        return false;
            }
        }

        int capacity = length >> 3;
        T[]? rented = null;
        Span<T> rejected = capacity <= 512 ? stackalloc T[capacity] :
            (rented = ArrayPool<T>.Shared.Rent(capacity)).AsSpan(0, capacity);
        try
        {
            int kept = 1, dropped = 0;
            for (int read = 1; read < length; read++)
            {
                T current = Unsafe.Add(ref data, read);
                T previous = Unsafe.Add(ref data, kept - 1);
                if (previous.CompareTo(current) <= 0)
                {
                    int end = read + 1;
                    if (Vector512.IsHardwareAccelerated && Vector512<T>.IsSupported)
                    {
                        int width = Vector512<T>.Count;
                        while (end <= length - width)
                        {
                            var a = Vector512.LoadUnsafe(ref data, (nuint)(end - 1));
                            var b = Vector512.LoadUnsafe(ref data, (nuint)end);
                            ulong mask = Vector512.ExtractMostSignificantBits(Vector512.LessThanOrEqual(a, b));
                            int run = Math.Min(width, BitOperations.TrailingZeroCount(~mask));
                            end += run;
                            if (run != width) break;
                        }
                    }
                    while (end < length &&
                        Unsafe.Add(ref data, end - 1).CompareTo(Unsafe.Add(ref data, end)) <= 0)
                        end++;
                    int count = end - read;
                    if (kept != read)
                        values.Slice(read, count).CopyTo(values.Slice(kept, count));
                    kept += count;
                    read = end - 1;
                    continue;
                }

                // An increasing cluster of high outliers can poison the
                // monotonic prefix. Remove a short suffix if doing so admits
                // the current value; otherwise it is probably a low outlier.
                const int Lookback = 8;
                int cut = kept - 1;
                int limit = Math.Max(0, kept - Lookback);
                while (cut > limit && Unsafe.Add(ref data, cut - 1).CompareTo(current) > 0)
                    cut--;

                bool removeSuffix = cut == 0 ||
                    Unsafe.Add(ref data, cut - 1).CompareTo(current) <= 0;
                int removeCount = removeSuffix ? kept - cut : 1;
                if (removeCount > capacity - dropped)
                {
                    // Nothing from the current read has been modified yet.
                    // Restore the full permutation before the radix fallback.
                    values[read..].CopyTo(values[kept..]);
                    rejected[..dropped].CopyTo(values[(length - dropped)..]);
                    return false;
                }

                if (removeSuffix)
                {
                    values.Slice(cut, removeCount).CopyTo(rejected.Slice(dropped, removeCount));
                    dropped += removeCount;
                    Unsafe.Add(ref data, cut) = current;
                    kept = cut + 1;
                }
                else
                {
                    rejected[dropped++] = current;
                }
            }

            if (dropped == 0) return true;
            Sort(rejected[..dropped]);
            // Backward merging cannot overwrite an unread kept element.
            int left = kept - 1, right = dropped - 1, output = length - 1;
            while (right >= 0)
            {
                T key = rejected[right--];
                int moved = 0;
                while (left >= 0 && moved < 8 && Unsafe.Add(ref data, left).CompareTo(key) > 0)
                {
                    Unsafe.Add(ref data, output--) = Unsafe.Add(ref data, left--);
                    moved++;
                }

                // Gallop across long runs, then move them with one memmove.
                if (moved == 8 && left >= 0 && Unsafe.Add(ref data, left).CompareTo(key) > 0)
                {
                    int hi = left + 1, step = 1;
                    while (step <= left && Unsafe.Add(ref data, left - step).CompareTo(key) > 0)
                    {
                        if (step > left / 2) { step = left + 1; break; }
                        step <<= 1;
                    }

                    int lo = Math.Max(0, left - step + 1);
                    while (lo < hi)
                    {
                        int mid = lo + ((hi - lo) >> 1);
                        if (Unsafe.Add(ref data, mid).CompareTo(key) <= 0) lo = mid + 1;
                        else hi = mid;
                    }

                    int count = left - lo + 1;
                    values.Slice(lo, count).CopyTo(values.Slice(output - count + 1, count));
                    output -= count;
                    left -= count;
                }
                Unsafe.Add(ref data, output--) = key;
            }
            return true;
        }
        finally
        {
            if (rented is not null) ArrayPool<T>.Shared.Return(rented);
        }
    }
}
