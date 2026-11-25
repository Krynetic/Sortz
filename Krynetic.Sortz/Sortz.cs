using System.Buffers;
using System.Globalization;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Krynetic.Sortz;

public static partial class Sortz
{
    private const int InsertionThreshold = 16;

    public static void Sort<T>(Span<T> values, IComparer<T>? comparer = null)
    {
        if (values.Length < 2)
            return;

        if (typeof(T) == typeof(int) &&
            (comparer is null || ReferenceEquals(comparer, Comparer<T>.Default)))
        {
            SortInt32(MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, int>(ref MemoryMarshal.GetReference(values)), values.Length));
            return;
        }

        if (typeof(T) == typeof(double) &&
            (comparer is null || ReferenceEquals(comparer, Comparer<T>.Default)))
        {
            SortDouble(MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, double>(ref MemoryMarshal.GetReference(values)), values.Length));
            return;
        }

        if ((comparer is null || ReferenceEquals(comparer, Comparer<T>.Default)) &&
            TrySortPrimitive(values))
            return;

        if (typeof(T) == typeof(string) && ReferenceEquals(comparer, StringComparer.Ordinal))
        {
            var strings = MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, string?>(ref MemoryMarshal.GetReference(values)), values.Length);
            if (!TryOrdered(strings, StringComparer.Ordinal))
                SortOrdinalPacked(strings);
            return;
        }

        comparer ??= Comparer<T>.Default;
        if (TryOrdered(values, comparer))
            return;

        if (typeof(T) == typeof(string) && values.Length >= 64 &&
            ReferenceEquals(comparer, Comparer<T>.Default))
        {
            var strings = MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, string?>(ref MemoryMarshal.GetReference(values)), values.Length);
            SortOrdinalPacked(strings);
            // Ordinal order often matches culture-sensitive order for identifiers.
            // Always verify against the actual comparer, never assume equivalence.
            if (IsCultureOrdered(strings))
                return;
            if (values.Length >= 2048 && TrySortCultureStrings(strings))
                return;
        }

        if (!typeof(T).IsValueType && ReferenceEquals(comparer, Comparer<T>.Default))
        {
            int depth = 2 * (BitOperations.Log2((uint)values.Length) + 1);
            if (ReferenceTraits<T>.Comparable)
            {
                int first = 0;
                for (int i = 0; i < values.Length; i++)
                    if (values[i] is null)
                        Swap(values, first++, i);
                SortComparableReference(values[first..], depth);
            }
            else
                SortReference(values, Comparer<T>.Default, depth);
            return;
        }

        IntroSort(values, comparer, 2 * (BitOperations.Log2((uint)values.Length) + 1));
    }

    private static class ReferenceTraits<T>
    {
        internal static readonly bool Comparable = typeof(IComparable<T>).IsAssignableFrom(typeof(T));
    }

    // All values are non-null, and the caller has established IComparable<T>.
    // Bind the pivot/key interface once, then reuse it for the entire scan.
    private static void SortComparableReference<T>(Span<T> values, int depth)
    {
        while (values.Length > 16)
        {
            if (depth-- == 0)
            {
                HeapSort(values, Comparer<T>.Default);
                return;
            }

            ref T data = ref MemoryMarshal.GetReference(values);
            int last = values.Length - 1, middle = last >> 1;
            T a = data, b = Unsafe.Add(ref data, middle), c = Unsafe.Add(ref data, last);
            if (((IComparable<T>)a!).CompareTo(b) > 0) (a, b) = (b, a);
            if (((IComparable<T>)a!).CompareTo(c) > 0) (a, c) = (c, a);
            if (((IComparable<T>)b!).CompareTo(c) > 0) (b, c) = (c, b);
            data = a;
            Unsafe.Add(ref data, middle) = Unsafe.Add(ref data, last - 1);
            Unsafe.Add(ref data, last - 1) = b;
            Unsafe.Add(ref data, last) = c;

            var pivot = (IComparable<T>)b!;
            int left;
            if (values.Length >= 256)
                left = PartitionComparableBlocks(ref data, last - 2, pivot);
            else
            {
                left = 0;
                int right = last - 1;
                while (true)
                {
                    do { left++; } while (left < last - 1 &&
                        pivot.CompareTo(Unsafe.Add(ref data, left)) > 0);
                    do { right--; } while (right > 0 &&
                        pivot.CompareTo(Unsafe.Add(ref data, right)) < 0);
                    if (left >= right) break;
                    ref T l = ref Unsafe.Add(ref data, left);
                    ref T r = ref Unsafe.Add(ref data, right);
                    (l, r) = (r, l);
                }
            }

            Unsafe.Add(ref data, last - 1) = Unsafe.Add(ref data, left);
            Unsafe.Add(ref data, left) = b;
            Span<T> lower = values[..left], upper = values[(left + 1)..];
            if (lower.Length < upper.Length)
            {
                SortComparableReference(lower, depth);
                values = upper;
            }
            else
            {
                SortComparableReference(upper, depth);
                values = lower;
            }
        }

        ref T input = ref MemoryMarshal.GetReference(values);
        for (int i = 1; i < values.Length; i++)
        {
            T key = Unsafe.Add(ref input, i);
            var comparable = (IComparable<T>)key!;
            int j = i;
            while (j > 0 && comparable.CompareTo(Unsafe.Add(ref input, j - 1)) < 0)
            {
                Unsafe.Add(ref input, j) = Unsafe.Add(ref input, j - 1);
                j--;
            }
            Unsafe.Add(ref input, j) = key;
        }
    }

    private static int PartitionComparableBlocks<T>(ref T data, int right, IComparable<T> pivot)
    {
        const int Block = 32;
        Span<int> indices = stackalloc int[Block * 2];
        ref int leftIndices = ref MemoryMarshal.GetReference(indices);
        ref int rightIndices = ref Unsafe.Add(ref leftIndices, Block);
        int left = 1, lc = 0, rc = 0, li = 0, ri = 0;

        while (right - left + 1 >= Block * 2)
        {
            if (lc == 0)
            {
                li = 0;
                for (int i = 0; i < Block; i++)
                {
                    Unsafe.Add(ref leftIndices, lc) = i;
                    lc += pivot.CompareTo(Unsafe.Add(ref data, left + i)) <= 0 ? 1 : 0;
                }
            }

            if (rc == 0)
            {
                ri = 0;
                for (int i = 0; i < Block; i++)
                {
                    Unsafe.Add(ref rightIndices, rc) = i;
                    rc += pivot.CompareTo(Unsafe.Add(ref data, right - i)) >= 0 ? 1 : 0;
                }
            }

            int swaps = Math.Min(lc, rc);
            for (int i = 0; i < swaps; i++)
            {
                ref T a = ref Unsafe.Add(ref data, left + Unsafe.Add(ref leftIndices, li + i));
                ref T b = ref Unsafe.Add(ref data, right - Unsafe.Add(ref rightIndices, ri + i));
                (a, b) = (b, a);
            }

            lc -= swaps; rc -= swaps; li += swaps; ri += swaps;
            if (lc == 0) left += Block;
            if (rc == 0) right -= Block;
        }

        // Any partially consumed block remains inside this scalar tail.
        while (left <= right)
        {
            while (left <= right && pivot.CompareTo(Unsafe.Add(ref data, left)) > 0)
                left++;
            while (left <= right && pivot.CompareTo(Unsafe.Add(ref data, right)) < 0)
                right--;
            if (left > right) break;
            ref T a = ref Unsafe.Add(ref data, left++);
            ref T b = ref Unsafe.Add(ref data, right--);
            (a, b) = (b, a);
        }
        return left;
    }

    private static void SortReference<T>(Span<T> values, Comparer<T> comparer, int depth)
    {
        while (values.Length > 16)
        {
            if (depth-- == 0)
            {
                HeapSort(values, comparer);
                return;
            }

            int last = values.Length - 1, middle = last >> 1;
            if (comparer.Compare(values[0], values[middle]) > 0) Swap(values, 0, middle);
            if (comparer.Compare(values[0], values[last]) > 0) Swap(values, 0, last);
            if (comparer.Compare(values[middle], values[last]) > 0) Swap(values, middle, last);
            Swap(values, middle, last - 1);
            T pivot = values[last - 1];

            int left = 0, right = last - 1;
            while (true)
            {
                do { left++; } while (left < last - 1 && comparer.Compare(values[left], pivot) < 0);
                do { right--; } while (right > 0 && comparer.Compare(pivot, values[right]) < 0);
                if (left >= right)
                    break;
                Swap(values, left, right);
            }

            Swap(values, left, last - 1);
            Span<T> lower = values[..left], upper = values[(left + 1)..];
            if (lower.Length < upper.Length)
            {
                SortReference(lower, comparer, depth);
                values = upper;
            }
            else
            {
                SortReference(upper, comparer, depth);
                values = lower;
            }
        }

        for (int i = 1; i < values.Length; i++)
        {
            T key = values[i];
            if (comparer.Compare(values[i - 1], key) <= 0)
                continue;
            int lo = 0, hi = i - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (comparer.Compare(values[mid], key) <= 0)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            values[lo..i].CopyTo(values[(lo + 1)..(i + 1)]);
            values[lo] = key;
        }
    }

    private static void SortInt32(Span<int> values)
    {
        int count = values.Length;

        // Ordered and reverse-ordered inputs require no scratch storage.
        int i = 1;
        while (i < count && values[i - 1] <= values[i])
            i++;
        if (i == count)
            return;

        i = 1;
        while (i < count && values[i - 1] >= values[i])
            i++;
        if (i == count)
        {
            values.Reverse();
            return;
        }

        if (count <= 32)
        {
            for (i = 1; i < count; i++)
            {
                int key = values[i];
                int j = i;
                while (j > 0 && values[j - 1] > key)
                {
                    values[j] = values[j - 1];
                    j--;
                }

                values[j] = key;
            }
            return;
        }

        if (count is >= 4096 and <= 65535 &&
            (TryFixedBucketInt32(values) || TryBucketInt32(values)))
            return;

        if (Vector512.IsHardwareAccelerated && count <= 1024)
        {
            SimdSort.SortInt32(values);
            return;
        }

        if (count is >= 4096 and <= 65535)
            SortInt32CompactRadix(values);
        else if (count >= 4096)
            SortInt32Wide(values);
        else
            SortInt32Lsd(values);
    }

    // A single-pass distribution into fixed-capacity buckets avoids both
    // the counting/prefix pass and the dependent scatter offsets. Abandon
    // the attempt before touching the input if any bucket is too dense.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool TryFixedBucketInt32(Span<int> values)
    {
        int length = values.Length;
        // Bound the scratch working set; larger arrays use compact MSD.
        if (length > 16384) return false;
        const int Capacity = 16;
        int width = BitOperations.Log2((uint)length) - 1;
        int bucketCount = 1 << width;
        int shift = 32 - width;
        Span<byte> counts = stackalloc byte[bucketCount];
        counts.Clear();
        int[] rented = ArrayPool<int>.Shared.Rent(bucketCount * Capacity);
        try
        {
            ref byte h = ref MemoryMarshal.GetReference(counts);
            ref int source = ref MemoryMarshal.GetReference(values);
            ref int scratch = ref MemoryMarshal.GetArrayDataReference(rented);

            for (int i = 0; i < length; i++)
            {
                int key = Unsafe.Add(ref source, i);
                int bucket = (int)(((uint)key ^ 0x80000000u) >> shift);
                ref byte count = ref Unsafe.Add(ref h, bucket);
                int slot = count;
                if (slot == Capacity) return false;
                Unsafe.Add(ref scratch, bucket * Capacity + slot) = key;
                count = (byte)(slot + 1);
            }

            int written = 0;
            for (int b = 0; b < bucketCount; b++)
            {
                int count = Unsafe.Add(ref h, b);
                if (count == 0) continue;
                ref int a = ref Unsafe.Add(ref scratch, b * Capacity);
                ref int dst = ref Unsafe.Add(ref source, written);
                if (count == 1)
                    dst = a;
                else if (count == 2)
                {
                    int x = a, y = Unsafe.Add(ref a, 1);
                    dst = Math.Min(x, y);
                    Unsafe.Add(ref dst, 1) = Math.Max(x, y);
                }
                else if (count == 3)
                {
                    int x = a, y = Unsafe.Add(ref a, 1), z = Unsafe.Add(ref a, 2);
                    int lo = Math.Min(x, y), hi = Math.Max(x, y);
                    dst = Math.Min(lo, z);
                    Unsafe.Add(ref dst, 1) = Math.Max(lo, Math.Min(hi, z));
                    Unsafe.Add(ref dst, 2) = Math.Max(hi, z);
                }
                else if (count == 4)
                {
                    int x = a, y = Unsafe.Add(ref a, 1), z = Unsafe.Add(ref a, 2), w = Unsafe.Add(ref a, 3);
                    int l0 = Math.Min(x, y), h0 = Math.Max(x, y);
                    int l1 = Math.Min(z, w), h1 = Math.Max(z, w);
                    int m0 = Math.Max(l0, l1), m1 = Math.Min(h0, h1);
                    dst = Math.Min(l0, l1);
                    Unsafe.Add(ref dst, 1) = Math.Min(m0, m1);
                    Unsafe.Add(ref dst, 2) = Math.Max(m0, m1);
                    Unsafe.Add(ref dst, 3) = Math.Max(h0, h1);
                }
                else if (Vector512.IsHardwareAccelerated && count >= 8)
                {
                    // All sixteen lanes are within the allocated bucket.
                    // Mask unoccupied lanes before sorting, and store only
                    // the valid elements, preserving actual int.MaxValue keys.
                    SimdSort.SortBucketInt32(ref a, ref dst, count);
                }
                else
                {
                    dst = a;
                    for (int i = 1; i < count; i++)
                    {
                        int carry = Unsafe.Add(ref a, i);
                        for (int j = 0; j < i; j++)
                        {
                            ref int item = ref Unsafe.Add(ref dst, j);
                            int previous = item;
                            item = Math.Min(previous, carry);
                            carry = Math.Max(previous, carry);
                        }
                        Unsafe.Add(ref dst, i) = carry;
                    }
                }
                written += count;
            }
            return true;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    // MSD distribution into small, cache-local buckets. Distribute full keys once,
    // then sort each bucket directly into the original destination. Reject skew before modifying the input.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool TryBucketInt32(Span<int> values)
    {
        int length = values.Length;
        int width = Math.Clamp(BitOperations.Log2((uint)length) - 1, 8, 13);
        int bucketCount = 1 << width;
        int shift = 32 - width;
        Span<ushort> counts = stackalloc ushort[bucketCount];
        counts.Clear();
        ref ushort h = ref MemoryMarshal.GetReference(counts);
        ref int source = ref MemoryMarshal.GetReference(values);
        for (int i = 0; i < length; i++)
        {
            uint key = (uint)Unsafe.Add(ref source, i) ^ 0x80000000u;
            Unsafe.Add(ref h, (int)(key >> shift))++;
        }

        int total = 0;
        for (int b = 0; b < bucketCount; b++)
        {
            int frequency = Unsafe.Add(ref h, b);
            if (frequency > 24) return false;
            Unsafe.Add(ref h, b) = (ushort)total;
            total += frequency;
        }

        int[]? rented = null;
        Span<int> scratch = length <= 16384 ? stackalloc int[length] :
            (rented = ArrayPool<int>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            ref int dst = ref MemoryMarshal.GetReference(scratch);
            // Reuse the stable four-way scatter already used by projected keys.
            ScatterProjectedCompact(values, scratch, counts, shift,
                (uint)(bucketCount - 1), (uint)(bucketCount >> 1));

            int start = 0;
            for (int b = 0; b < bucketCount; b++)
            {
                int end = Unsafe.Add(ref h, b);
                int count = end - start;
                if (count == 0) continue;
                ref int a = ref Unsafe.Add(ref dst, start);
                ref int output = ref Unsafe.Add(ref source, start);
                int x = a;
                if (count == 1)
                    output = x;
                else if (count == 2)
                {
                    int y = Unsafe.Add(ref a, 1);
                    output = Math.Min(x, y);
                    Unsafe.Add(ref output, 1) = Math.Max(x, y);
                }
                else if (count == 3)
                {
                    int y = Unsafe.Add(ref a, 1), z = Unsafe.Add(ref a, 2);
                    int low = Math.Min(x, y), high = Math.Max(x, y);
                    output = Math.Min(low, z);
                    Unsafe.Add(ref output, 1) = Math.Max(low, Math.Min(high, z));
                    Unsafe.Add(ref output, 2) = Math.Max(high, z);
                }
                else if (count == 4)
                {
                    int y = Unsafe.Add(ref a, 1), z = Unsafe.Add(ref a, 2), w = Unsafe.Add(ref a, 3);
                    int l0 = Math.Min(x, y), h0 = Math.Max(x, y);
                    int l1 = Math.Min(z, w), h1 = Math.Max(z, w);
                    int m0 = Math.Max(l0, l1), m1 = Math.Min(h0, h1);
                    output = Math.Min(l0, l1);
                    Unsafe.Add(ref output, 1) = Math.Min(m0, m1);
                    Unsafe.Add(ref output, 2) = Math.Max(m0, m1);
                    Unsafe.Add(ref output, 3) = Math.Max(h0, h1);
                }
                else
                {
                    output = x;
                    for (int i = 1; i < count; i++)
                    {
                        int key = Unsafe.Add(ref a, i);
                        int j = i;
                        while (j > 0 && Unsafe.Add(ref output, j - 1) > key)
                        {
                            Unsafe.Add(ref output, j) = Unsafe.Add(ref output, j - 1);
                            j--;
                        }
                        Unsafe.Add(ref output, j) = key;
                    }
                }
                start = end;
            }
            return true;
        }
        finally
        {
            if (rented is not null) ArrayPool<int>.Shared.Return(rented);
        }
    }

    // Find the next adjacent equal prefix without visiting each distinct key
    // individually. The overlapping loads compare sixteen adjacent pairs.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextProjectedCollision(ref int keys, int index, int length, uint indexMask)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var prefixMask = Vector512.Create(unchecked((int)~indexMask));
            for (; index <= length - 16; index += 16)
            {
                var current = Vector512.LoadUnsafe(ref keys, (nuint)index);
                var previous = Vector512.LoadUnsafe(ref keys, (nuint)(index - 1));
                uint matches = (uint)Vector512.ExtractMostSignificantBits(
                    Vector512.Equals((current ^ previous) & prefixMask, Vector512<int>.Zero));
                if (matches != 0)
                    return index + BitOperations.TrailingZeroCount(matches);
            }
        }

        for (; index < length; index++)
            if (((uint)(Unsafe.Add(ref keys, index) ^ Unsafe.Add(ref keys, index - 1)) & ~indexMask) == 0)
                break;
        return index;
    }

    // Packed keys contain a source index in their low bits. Only the prefix
    // participates in ordering; callers repair equal-prefix groups exactly.
    private static void SortProjectedInt32(Span<int> keys, int indexBits)
    {
        int length = keys.Length;
        if (length is < 4096 or > 65535)
        {
            SortInt32(keys);
            return;
        }

        int lowBits = (33 - indexBits) >> 1;
        int highBits = 32 - indexBits - lowBits;
        int lowCount = 1 << lowBits, highCount = 1 << highBits;
        int highShift = indexBits + lowBits;
        uint lowMask = (uint)(lowCount - 1);
        uint highMask = (uint)(highCount - 1), sign = (uint)(highCount >> 1);
        Span<ushort> histogram = stackalloc ushort[lowCount + highCount];
        histogram.Clear();
        ref ushort counts = ref MemoryMarshal.GetReference(histogram);
        ref int input = ref MemoryMarshal.GetReference(keys);
        for (int i = 0; i < length; i++)
        {
            uint key = (uint)Unsafe.Add(ref input, i);
            Unsafe.Add(ref counts, (int)((key >> indexBits) & lowMask))++;
            Unsafe.Add(ref counts, lowCount + (int)((key >> highShift) ^ sign))++;
        }

        int[]? rented = null;
        Span<int> scratch = length <= 16384 ? stackalloc int[length] :
            (rented = ArrayPool<int>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            Span<ushort> low = histogram[..lowCount], high = histogram[lowCount..];
            int offset = 0;
            for (int i = 0; i < low.Length; i++)
            {
                int count = low[i];
                low[i] = (ushort)offset;
                offset += count;
            }
            offset = 0;
            for (int i = 0; i < high.Length; i++)
            {
                int count = high[i];
                high[i] = (ushort)offset;
                offset += count;
            }
            ScatterProjectedCompact(keys, scratch, low, indexBits, lowMask, 0);
            ScatterProjectedCompact(scratch, keys, high, highShift, highMask, sign);
        }
        finally
        {
            if (rented is not null) ArrayPool<int>.Shared.Return(rented);
        }
    }

    // For at most 65,535 elements, both frequencies and exclusive offsets
    // fit in 16 bits. This halves histogram footprint without truncating keys.
    private static void SortInt32CompactRadix(Span<int> values)
    {
        int count = values.Length;
        Span<ushort> counts = stackalloc ushort[2048 + 2048 + 1024];
        counts.Clear();
        ref ushort h = ref MemoryMarshal.GetReference(counts);
        ref int source = ref MemoryMarshal.GetReference(values);
        for (int i = 0; i < count; i++)
        {
            uint key = (uint)Unsafe.Add(ref source, i);
            Unsafe.Add(ref h, (int)(key & 2047))++;
            Unsafe.Add(ref h, 2048 + (int)((key >> 11) & 2047))++;
            Unsafe.Add(ref h, 4096 + (int)((key >> 22) ^ 512))++;
        }

        int[]? rented = null;
        Span<int> scratch = count <= 16384 ? stackalloc int[count] :
            (rented = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            bool inScratch = false;
            uint first = (uint)source;
            for (int digit = 0; digit < 3; digit++)
            {
                int shift = digit * 11;
                Span<ushort> histogram = counts.Slice(digit * 2048, digit == 2 ? 1024 : 2048);
                uint mask = (uint)(histogram.Length - 1);
                uint sign = digit == 2 ? 512u : 0u;
                if (histogram[(int)(((first >> shift) & mask) ^ sign)] == count)
                    continue;

                int sum = 0;
                for (int b = 0; b < histogram.Length; b++)
                {
                    int frequency = histogram[b];
                    histogram[b] = (ushort)sum;
                    sum += frequency;
                }

                if (inScratch)
                    ScatterCompact(scratch, values, histogram, shift, mask, sign);
                else
                    ScatterCompact(values, scratch, histogram, shift, mask, sign);
                inScratch = !inScratch;
            }
            if (inScratch) scratch.CopyTo(values);
        }
        finally
        {
            if (rented is not null) ArrayPool<int>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ScatterCompact(ReadOnlySpan<int> source, Span<int> target,
        Span<ushort> histogram, int shift, uint mask, uint sign)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref int output = ref MemoryMarshal.GetReference(target);
        ref ushort offsets = ref MemoryMarshal.GetReference(histogram);
        for (int i = 0; i < source.Length; i++)
        {
            int value = Unsafe.Add(ref input, i);
            int bucket = (int)((((uint)value >> shift) & mask) ^ sign);
            ref ushort position = ref Unsafe.Add(ref offsets, bucket);
            int index = position;
            position = (ushort)(index + 1);
            Unsafe.Add(ref output, index) = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ScatterProjectedCompact(ReadOnlySpan<int> source, Span<int> target,
        Span<ushort> histogram, int shift, uint mask, uint sign)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref int output = ref MemoryMarshal.GetReference(target);
        ref ushort offsets = ref MemoryMarshal.GetReference(histogram);
        int i = 0, length = source.Length;
        for (; i <= length - 4; i += 4)
        {
            int a = Unsafe.Add(ref input, i);
            int b = Unsafe.Add(ref input, i + 1);
            int c = Unsafe.Add(ref input, i + 2);
            int d = Unsafe.Add(ref input, i + 3);
            ref ushort pa = ref Unsafe.Add(ref offsets, (int)((((uint)a >> shift) & mask) ^ sign));
            ref ushort pb = ref Unsafe.Add(ref offsets, (int)((((uint)b >> shift) & mask) ^ sign));
            ref ushort pc = ref Unsafe.Add(ref offsets, (int)((((uint)c >> shift) & mask) ^ sign));
            ref ushort pd = ref Unsafe.Add(ref offsets, (int)((((uint)d >> shift) & mask) ^ sign));
            // Counters may alias: advance each in original input order.
            int ia = pa; pa = (ushort)(ia + 1);
            int ib = pb; pb = (ushort)(ib + 1);
            int ic = pc; pc = (ushort)(ic + 1);
            int id = pd; pd = (ushort)(id + 1);
            Unsafe.Add(ref output, ia) = a;
            Unsafe.Add(ref output, ib) = b;
            Unsafe.Add(ref output, ic) = c;
            Unsafe.Add(ref output, id) = d;
        }
        for (; i < length; i++)
        {
            int value = Unsafe.Add(ref input, i);
            int bucket = (int)((((uint)value >> shift) & mask) ^ sign);
            ref ushort position = ref Unsafe.Add(ref offsets, bucket);
            int index = position;
            position = (ushort)(index + 1);
            Unsafe.Add(ref output, index) = value;
        }
    }

    private static void SortInt32Wide(Span<int> values)
    {
        int count = values.Length;
        Span<int> counts = stackalloc int[2048 + 2048 + 1024];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        foreach (int value in values)
        {
            uint key = (uint)value ^ 0x8000_0000u;
            Unsafe.Add(ref h, (int)(key & 2047))++;
            Unsafe.Add(ref h, 2048 + (int)((key >> 11) & 2047))++;
            Unsafe.Add(ref h, 4096 + (int)(key >> 22))++;
        }

        int[]? rented = null;
        Span<int> scratch = count <= 16384 ? stackalloc int[count] :
            (rented = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            bool inScratch = false;
            uint first = (uint)values[0] ^ 0x8000_0000u;
            for (int digit = 0; digit < 3; digit++)
            {
                int shift = digit * 11;
                Span<int> histogram = counts.Slice(digit * 2048, digit == 2 ? 1024 : 2048);
                uint mask = (uint)(histogram.Length - 1);
                if (histogram[(int)((first >> shift) & mask)] == count)
                    continue;

                int sum = 0;
                for (int b = 0; b < histogram.Length; b++)
                {
                    int frequency = histogram[b];
                    histogram[b] = sum;
                    sum += frequency;
                }

                if (inScratch)
                    ScatterWide(scratch, values, histogram, shift, mask);
                else
                    ScatterWide(values, scratch, histogram, shift, mask);
                inScratch = !inScratch;
            }

            if (inScratch) scratch.CopyTo(values);
        }
        finally
        {
            if (rented is not null) ArrayPool<int>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ScatterWide(ReadOnlySpan<int> source, Span<int> target,
        Span<int> histogram, int shift, uint mask)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref int output = ref MemoryMarshal.GetReference(target);
        ref int offsets = ref MemoryMarshal.GetReference(histogram);
        for (int i = 0; i < source.Length; i++)
        {
            int value = Unsafe.Add(ref input, i);
            int bucket = (int)((((uint)value ^ 0x8000_0000u) >> shift) & mask);
            Unsafe.Add(ref output, Unsafe.Add(ref offsets, bucket)++) = value;
        }
    }

    private static void SortInt32Lsd(Span<int> values)
    {
        int count = values.Length;
        int i;
        // Build all four byte histograms in one traversal.
        Span<int> counts = stackalloc int[4 * 256];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        ref int v = ref MemoryMarshal.GetReference(values);
        for (i = 0; i < count; i++)
        {
            uint key = (uint)Unsafe.Add(ref v, i);
            Unsafe.Add(ref h, (int)(key & 255))++;
            Unsafe.Add(ref h, 256 + (int)((key >> 8) & 255))++;
            Unsafe.Add(ref h, 512 + (int)((key >> 16) & 255))++;
            Unsafe.Add(ref h, 768 + (int)((key >> 24) ^ 128))++;
        }

        int[]? rented = null;
        Span<int> scratch = count <= 16384
            ? stackalloc int[count]
            : (rented = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);

        try
        {
            // Skip uniform digits. The highest byte flips the sign bit.
            bool inScratch = false;
            for (int digit = 0; digit < 4; digit++)
            {
                int shift = digit * 8;
                uint sign = digit == 3 ? 128u : 0u;
                Span<int> histogram = counts.Slice(digit * 256, 256);
                int bucket = (int)((((uint)values[0] >> shift) & 255) ^ sign);
                if (histogram[bucket] == count)
                    continue;

                int sum = 0;
                for (int b = 0; b < 256; b++)
                {
                    int frequency = histogram[b];
                    histogram[b] = sum;
                    sum += frequency;
                }

                if (inScratch)
                    ScatterInt32(scratch, values, histogram, shift, sign);
                else
                    ScatterInt32(values, scratch, histogram, shift, sign);

                inScratch = !inScratch;
            }

            if (inScratch)
                scratch.CopyTo(values);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<int>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ScatterInt32(ReadOnlySpan<int> source, Span<int> target,
        Span<int> histogram, int shift, uint sign)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref int output = ref MemoryMarshal.GetReference(target);
        ref int offsets = ref MemoryMarshal.GetReference(histogram);
        for (int i = 0; i < source.Length; i++)
        {
            int value = Unsafe.Add(ref input, i);
            int bucket = (int)((((uint)value >> shift) & 255) ^ sign);
            int position = Unsafe.Add(ref offsets, bucket)++;
            Unsafe.Add(ref output, position) = value;
        }
    }

    private readonly record struct CultureEntry(string? Value, int Offset, int Length, ulong Prefix);

    private readonly struct CultureEntryComparer(byte[] keys) : IComparer<CultureEntry>
    {
        public int Compare(CultureEntry x, CultureEntry y)
        {
            if (x.Value is null)
                return y.Value is null ? 0 : -1;
            if (y.Value is null)
                return 1;

            int prefix = x.Prefix.CompareTo(y.Prefix);
            if (prefix != 0)
                return prefix;

            return keys.AsSpan(x.Offset, x.Length)
                .SequenceCompareTo(keys.AsSpan(y.Offset, y.Length));
        }
    }

    private static bool TrySortCultureStrings(Span<string?> values)
    {
        const int MaxKeyBytes = 64 * 1024 * 1024;
        CompareInfo compareInfo = CultureInfo.CurrentCulture.CompareInfo;
        CultureEntry[] rentedEntries = ArrayPool<CultureEntry>.Shared.Rent(values.Length);
        Span<CultureEntry> entries = rentedEntries.AsSpan(0, values.Length);
        byte[]? keys = null;
        try
        {
            // Reserve a conservative capacity and generate each key directly into
            // the pooled buffer. Fall back to comparison sorting if it is insufficient.
            long capacity = 0;
            foreach (string? value in values)
            {
                if (value is not null)
                    capacity += 64L + 12L * value.Length;
                if (capacity > MaxKeyBytes)
                    return false;
            }

            keys = ArrayPool<byte>.Shared.Rent((int)capacity);
            int total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string? value = values[i];
                if (value is null)
                {
                    entries[i] = new CultureEntry(null, 0, 0, 0);
                    continue;
                }

                int length;
                try
                {
                    length = compareInfo.GetSortKey(value.AsSpan(), keys.AsSpan(total), CompareOptions.None);
                }
                catch (ArgumentException)
                {
                    return false;
                }

                ReadOnlySpan<byte> key = keys.AsSpan(total, length);
                ulong prefix = 0;
                if (length >= sizeof(ulong))
                    prefix = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(key);
                else
                    for (int j = 0; j < length; j++)
                        prefix |= (ulong)key[j] << (56 - 8 * j);

                entries[i] = new CultureEntry(value, total, length, prefix);
                total += length;
            }

            if (values.Length is >= 512 and <= 65535 &&
                TrySortCultureProjected(values, rentedEntries, keys))
                return true;

            IntroSort(entries, new CultureEntryComparer(keys),
                2 * (BitOperations.Log2((uint)entries.Length) + 1));

            for (int i = 0; i < values.Length; i++)
                values[i] = entries[i].Value;

            return true;
        }
        finally
        {
            entries.Clear();
            ArrayPool<CultureEntry>.Shared.Return(rentedEntries);
            if (keys is not null)
                ArrayPool<byte>.Shared.Return(keys);
        }
    }

    private readonly struct CultureIndexComparer(CultureEntry[] entries, byte[] sortKeys, uint indexMask) : IComparer<int>
    {
        public int Compare(int x, int y) =>
            new CultureEntryComparer(sortKeys).Compare(entries[(int)((uint)x & indexMask)],
                entries[(int)((uint)y & indexMask)]);
    }

    // Keep the collation entries stationary and sort only compact integer keys.
    // A prefix collision is always resolved using the complete collation keys.
    private static bool TrySortCultureProjected(Span<string?> values, CultureEntry[] entries, byte[] sortKeys)
    {
        int length = values.Length;
        int indexBits = BitOperations.Log2((uint)(length - 1)) + 1;
        uint indexMask = (1u << indexBits) - 1;
        uint prefixMask = ~indexMask;
        int[]? rented = null;
        Span<int> keys = length <= 16384 ? stackalloc int[length] :
            (rented = ArrayPool<int>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            ref int data = ref MemoryMarshal.GetReference(keys);
            ref CultureEntry source = ref MemoryMarshal.GetArrayDataReference(entries);
            uint first = 0, varying = 0;
            for (int i = 0; i < length; i++)
            {
                uint prefix = ((uint)(Unsafe.Add(ref source, i).Prefix >> 32) ^ 0x80000000u) & prefixMask;
                if (i == 0) first = prefix;
                varying |= first ^ prefix;
                Unsafe.Add(ref data, i) = (int)(prefix | (uint)i);
            }
            if (BitOperations.PopCount(varying) < 4)
                return false;

            SortProjectedInt32(keys, indexBits);
            int maxCollision = Math.Max(64, length / 8);
            for (int i = maxCollision; i < length; i += maxCollision)
                if (((uint)(keys[i] ^ keys[i - maxCollision]) & prefixMask) == 0)
                    return false;

            var comparer = new CultureIndexComparer(entries, sortKeys, indexMask);
            for (int i = 1; i < length; i++)
            {
                i = NextProjectedCollision(ref data, i, length, indexMask);
                if (i == length) break;
                int start = i - 1, end = i + 1;
                uint prefix = (uint)Unsafe.Add(ref data, start) & prefixMask;
                while (end < length && ((uint)Unsafe.Add(ref data, end) & prefixMask) == prefix)
                    end++;
                int count = end - start;
                if (count == 2)
                {
                    ref int a = ref Unsafe.Add(ref data, start);
                    ref int b = ref Unsafe.Add(ref data, i);
                    if (comparer.Compare(a, b) > 0) (a, b) = (b, a);
                }
                else
                    IntroSort(keys.Slice(start, count), comparer, 2 * (BitOperations.Log2((uint)count) + 1));
                i = end;
            }

            for (int i = 0; i < length; i++)
                values[i] = Unsafe.Add(ref source, (int)((uint)Unsafe.Add(ref data, i) & indexMask)).Value;
            return true;
        }
        finally { if (rented is not null) ArrayPool<int>.Shared.Return(rented); }
    }

    // Sort prefix and source index together as an unsigned integer. All
    // collisions are resolved using full ordinal comparisons after permutation.
    private static void SortOrdinalPacked(Span<string?> values)
    {
        int count = values.Length;
        if (count < 64)
        {
            SortOrdinal(values, 0, 64);
            return;
        }

        if (count <= 4096 && Bmi2.X64.IsSupported && BitConverter.IsLittleEndian &&
            TrySortOrdinal32(values))
            return;

        if (count is > 4096 and <= 65535 && Bmi2.X64.IsSupported &&
            BitConverter.IsLittleEndian && TrySortOrdinal30(values))
            return;

        int indexBits = BitOperations.Log2((uint)(count - 1)) + 1;
        ulong indexMask = (1UL << indexBits) - 1;
        int units = (64 - indexBits) / 8;
        ulong prefixMask = ulong.MaxValue << (64 - units * 8);
        ulong[]? rented = null;
        Span<ulong> keys = count <= 8192 ? stackalloc ulong[count] :
            (rented = ArrayPool<ulong>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            bool latin1 = true;
            for (int i = 0; i < count; i++)
            {
                string? value = values[i];
                ulong prefix = 0;
                if (value is not null && value.Length >= 8 &&
                    Bmi2.X64.IsSupported && BitConverter.IsLittleEndian)
                {
                    ref byte first = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan()));
                    ulong a = Unsafe.ReadUnaligned<ulong>(ref first);
                    ulong b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref first, 8));
                    if (((a | b) & 0xff00ff00ff00ff00UL) != 0)
                    {
                        latin1 = false;
                        break;
                    }
                    const ulong mask = 0x00ff00ff00ff00ffUL;
                    prefix = BinaryPrimitives.ReverseEndianness(Bmi2.X64.ParallelBitExtract(a, mask) |
                        (Bmi2.X64.ParallelBitExtract(b, mask) << 32));
                }
                else if (value is not null)
                {
                    for (int j = 0; j < Math.Min(units, value.Length); j++)
                    {
                        if (value[j] > 255) { latin1 = false; break; }
                        prefix |= (ulong)value[j] << (56 - 8 * j);
                    }
                    if (!latin1) break;
                }
                keys[i] = (prefix & prefixMask) | (uint)i;
            }

            if (!latin1)
            {
                units = (64 - indexBits) / 16;
                for (int i = 0; i < count; i++)
                {
                    string? value = values[i];
                    ulong prefix = 0;
                    if (value is not null)
                        for (int j = 0; j < Math.Min(units, value.Length); j++)
                            prefix |= (ulong)value[j] << (48 - 16 * j);
                    keys[i] = prefix | (uint)i;
                }
            }

            SortUInt64(keys, 8 - units * (latin1 ? 1 : 2));

            // The low index bits are payload, not part of the ordering.
            // Sequential reference writes avoid the cache misses of permutation cycles.
            string?[] references = ArrayPool<string?>.Shared.Rent(count);
            Span<string?> original = references.AsSpan(0, count);
            try
            {
                values.CopyTo(original);
                for (int i = 0; i < count; i++)
                    values[i] = original[(int)(keys[i] & indexMask)];
            }
            finally
            {
                original.Clear();
                ArrayPool<string?>.Shared.Return(references);
            }

            for (int start = 0; start < count;)
            {
                ulong prefix = keys[start] & ~indexMask;
                int end = start + 1;
                while (end < count && (keys[end] & ~indexMask) == prefix)
                    end++;
                if (end - start > 1)
                    SortOrdinalBuckets(values[start..end], 0, 32);
                start = end;
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    // Six UTF-16 code units compressed to a 30-bit monotone prefix.
    // The range is verified on all elements, and equal keys are resolved ordinally.
    private static bool TrySortOrdinal30(Span<string?> values)
    {
        int count = values.Length;
        int min = char.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            string? value = values[i];
            if (value is null || value.Length < 6)
                return false;
            for (int j = 0; j < 6; j++)
                min = Math.Min(min, value[j]);
        }

        const ulong lanes = 0x0001000100010001UL;
        const ulong mask = 31UL * lanes;
        ulong repeatedBase = (ulong)(min & ~15) * lanes;
        ulong reversedMask = BinaryPrimitives.ReverseEndianness(mask);
        int indexBits = BitOperations.Log2((uint)(count - 1)) + 1;
        ulong indexMask = (1UL << indexBits) - 1;
        ulong[] rented = ArrayPool<ulong>.Shared.Rent(count);
        Span<ulong> keys = rented.AsSpan(0, count);
        try
        {
            for (int i = 0; i < count; i++)
            {
                string? value = values[i];
                if (value is null || value.Length < 6)
                    return false;
                ref byte first = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan()));
                ulong a = Unsafe.ReadUnaligned<ulong>(ref first) - repeatedBase;
                ulong b = (ulong)Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref first, 8)) - (repeatedBase & uint.MaxValue);
                if ((a & ~mask) != 0 || (b & ~(mask & uint.MaxValue)) != 0)
                    return false;
                ulong prefix = (Bmi2.X64.ParallelBitExtract(BinaryPrimitives.ReverseEndianness(a), reversedMask) << 10)
                    | Bmi2.X64.ParallelBitExtract(BinaryPrimitives.ReverseEndianness(b), BinaryPrimitives.ReverseEndianness(mask & uint.MaxValue));
                keys[i] = (prefix << indexBits) | (uint)i;
            }

            SortPrefix30(keys, indexBits);
            string?[] references = ArrayPool<string?>.Shared.Rent(count);
            Span<string?> original = references.AsSpan(0, count);
            try
            {
                values.CopyTo(original);
                for (int i = 0; i < count; i++)
                    values[i] = original[(int)(keys[i] & indexMask)];
            }
            finally
            {
                original.Clear();
                ArrayPool<string?>.Shared.Return(references);
            }

            ref ulong keyData = ref MemoryMarshal.GetReference(keys);
            for (int next = 1; next < count; next++)
            {
                next = NextOrdinalCollision64(ref keyData, next, count, indexMask);
                if (next == count) break;
                int start = next - 1, end = next + 1;
                ulong prefix = keys[start] >> indexBits;
                while (end < count && (keys[end] >> indexBits) == prefix)
                    end++;
                if (end - start == 2)
                {
                    if (string.CompareOrdinal(values[start], values[next]) > 0)
                        (values[start], values[next]) = (values[next], values[start]);
                }
                else
                    SortOrdinalBuckets(values[start..end], 6, 32);
                next = end;
            }
            return true;
        }
        finally { ArrayPool<ulong>.Shared.Return(rented); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NextOrdinalCollision64(ref ulong keys, int index, int length, ulong indexMask)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            var mask = Vector512.Create(~indexMask);
            for (; index <= length - 8; index += 8)
            {
                var a = Vector512.LoadUnsafe(ref keys, (nuint)index);
                var b = Vector512.LoadUnsafe(ref keys, (nuint)(index - 1));
                uint matches = (uint)Vector512.ExtractMostSignificantBits(
                    Vector512.Equals((a ^ b) & mask, Vector512<ulong>.Zero));
                if (matches != 0) return index + BitOperations.TrailingZeroCount(matches);
            }
        }
        for (; index < length; index++)
            if (((Unsafe.Add(ref keys, index) ^ Unsafe.Add(ref keys, index - 1)) & ~indexMask) == 0)
                break;
        return index;
    }

    private static void SortPrefix30(Span<ulong> keys, int shift)
    {
        Span<int> counts = stackalloc int[3072];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        ref ulong source = ref MemoryMarshal.GetReference(keys);
        for (int i = 0; i < keys.Length; i++)
        {
            ulong k = Unsafe.Add(ref source, i) >> shift;
            Unsafe.Add(ref h, (int)(k & 1023))++;
            Unsafe.Add(ref h, 1024 + (int)((k >> 10) & 1023))++;
            Unsafe.Add(ref h, 2048 + (int)((k >> 20) & 1023))++;
        }
        ulong[] rented = ArrayPool<ulong>.Shared.Rent(keys.Length);
        Span<ulong> scratch = rented.AsSpan(0, keys.Length);
        try
        {
            for (int digit = 0; digit < 3; digit++)
            {
                Span<int> histogram = counts.Slice(digit * 1024, 1024);
                int sum = 0;
                for (int b = 0; b < histogram.Length; b++)
                {
                    int frequency = histogram[b];
                    histogram[b] = sum;
                    sum += frequency;
                }
                if ((digit & 1) == 0)
                    ScatterUInt64Wide(keys, scratch, histogram, shift + digit * 10, 1023);
                else
                    ScatterUInt64Wide(scratch, keys, histogram, shift + digit * 10, 1023);
            }
            scratch.CopyTo(keys);
        }
        finally { ArrayPool<ulong>.Shared.Return(rented); }
    }

    // Compact four-code-unit prefixes when the observed alphabet occupies a
    // narrow contiguous range. The range is checked for every key before use.
    [InlineArray(128)]
    private struct SmallReferenceBuffer
    {
        private string? element;
    }

    private static bool TrySortOrdinal32(Span<string?> values)
    {
        int count = values.Length;
        int indexBits = BitOperations.Log2((uint)(count - 1)) + 1;
        const int units = 4;
        int width = (31 - indexBits) / units;
        if (width < 4)
            return false;

        int min = char.MaxValue;
        for (int i = 0; i < Math.Min(count, 8); i++)
        {
            string? value = values[i];
            if (value is null || value.Length < 4)
                return false;
            for (int j = 0; j < units; j++)
                min = Math.Min(min, value[j]);
        }

        uint symbolMask = (1u << width) - 1;
        ulong baseValue = (ulong)(min & ~(int)(symbolMask >> 1));
        const ulong lanes = 0x0001000100010001UL;
        ulong repeatedBase = baseValue * lanes;
        ulong mask = (ulong)symbolMask * lanes;
        ulong reversedMask = BinaryPrimitives.ReverseEndianness(mask);
        uint indexMask = (1u << indexBits) - 1;
        int[]? rentedKeys = null;
        Span<int> keys = count <= 8192 ? stackalloc int[count] :
            (rentedKeys = ArrayPool<int>.Shared.Rent(count)).AsSpan(0, count);
        try
        {

        for (int i = 0; i < count; i++)
        {
            string? value = values[i];
            if (value is null || value.Length < 4)
                return false;
            ref byte data = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan()));
            ulong adjusted = Unsafe.ReadUnaligned<ulong>(ref data) - repeatedBase;
            if ((adjusted & ~mask) != 0)
                return false;
            ulong prefix = Bmi2.X64.ParallelBitExtract(
                BinaryPrimitives.ReverseEndianness(adjusted), reversedMask);
            keys[i] = (int)((prefix << indexBits) | (uint)i);
        }

        if (count <= 256) SortInt32(keys);
        else SortPrefix32(keys, indexBits, units * width);

        if (count <= 128)
        {
            SmallReferenceBuffer buffer = default;
            Span<string?> original = buffer;
            values.CopyTo(original);
            for (int i = 0; i < count; i++)
                values[i] = original[(int)((uint)keys[i] & indexMask)];
        }
        else
        {
            string?[] rented = ArrayPool<string?>.Shared.Rent(count);
            Span<string?> original = rented.AsSpan(0, count);
            try
            {
                values.CopyTo(original);
                for (int i = 0; i < count; i++)
                    values[i] = original[(int)((uint)keys[i] & indexMask)];
            }
            finally
            {
                original.Clear();
                ArrayPool<string?>.Shared.Return(rented);
            }
        }

        ref int keyData = ref MemoryMarshal.GetReference(keys);
        for (int i = 1; i < count; i++)
        {
            i = NextProjectedCollision(ref keyData, i, count, indexMask);
            if (i == count) break;
            int start = i - 1, end = i + 1;
            uint prefix = (uint)keys[start] >> indexBits;
            while (end < count && ((uint)keys[end] >> indexBits) == prefix)
                end++;
            if (end - start == 2)
            {
                if (string.CompareOrdinal(values[start], values[i]) > 0)
                    (values[start], values[i]) = (values[i], values[start]);
            }
            else
                SortOrdinalBuckets(values[start..end], units, 32);
            i = end;
        }

        return true;
        }
        finally
        {
            if (rentedKeys is not null) ArrayPool<int>.Shared.Return(rentedKeys);
        }
    }

    private static void SortPrefix32(Span<int> keys, int shift, int bits)
    {
        int lowBits = bits / 2, highBits = bits - lowBits;
        int loCount = 1 << lowBits, hiCount = 1 << highBits;
        Span<int> counts = stackalloc int[loCount + hiCount];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        ref int src = ref MemoryMarshal.GetReference(keys);
        uint mask = (uint)(loCount - 1);
        for (int i = 0; i < keys.Length; i++)
        {
            uint k = (uint)Unsafe.Add(ref src, i) >> shift;
            Unsafe.Add(ref h, (int)(k & mask))++;
            Unsafe.Add(ref h, loCount + (int)(k >> lowBits))++;
        }
        Span<int> lo = counts[..loCount], hi = counts[loCount..];
        for (int i = 0, sum = 0; i < lo.Length; i++)
        {
            int n = lo[i]; lo[i] = sum; sum += n;
        }
        for (int i = 0, sum = 0; i < hi.Length; i++)
        {
            int n = hi[i]; hi[i] = sum; sum += n;
        }
        int[]? rented = null;
        Span<int> scratch = keys.Length <= 8192 ? stackalloc int[keys.Length] :
            (rented = ArrayPool<int>.Shared.Rent(keys.Length)).AsSpan(0, keys.Length);
        try
        {
            ref int dst = ref MemoryMarshal.GetReference(scratch);
            for (int i = 0; i < keys.Length; i++)
            {
                int k = Unsafe.Add(ref src, i);
                Unsafe.Add(ref dst, Unsafe.Add(ref h, (int)(((uint)k >> shift) & mask))++) = k;
            }
            for (int i = 0; i < keys.Length; i++)
            {
                int k = Unsafe.Add(ref dst, i);
                Unsafe.Add(ref src, Unsafe.Add(ref h, loCount + (int)((uint)k >> (shift + lowBits)))++) = k;
            }
        }
        finally { if (rented is not null) ArrayPool<int>.Shared.Return(rented); }
    }

    // In-place MSD bucket distribution for Latin-1 code units, with a
    // general UTF-16 multikey fallback. Null and end-of-string sort first.
    private static void SortOrdinalBuckets(Span<string?> values, int index, int budget)
    {
        if (values.Length < 32 || budget == 0)
        {
            SortOrdinal(values, index, 64);
            return;
        }

        Span<int> ends = stackalloc int[258];
        Span<int> next = stackalloc int[258];
        while (budget-- > 0)
        {
            ends.Clear();
            int nonEmpty = 0, only = 0, min = 257, max = 0;
            foreach (string? value in values)
            {
                int digit = OrdinalDigit(value, index) + 2;
                if (digit >= ends.Length)
                {
                    SortOrdinal(values, index, 64);
                    return;
                }
                if (ends[digit]++ == 0)
                {
                    nonEmpty++;
                    only = digit;
                    min = Math.Min(min, digit);
                    max = Math.Max(max, digit);
                }
            }

            if (nonEmpty == 1)
            {
                if (only < 2)
                    return;
                index++;
                continue;
            }

            int sum = 0;
            for (int b = min; b <= max; b++)
            {
                next[b] = sum;
                sum += ends[b];
                ends[b] = sum;
            }

            for (int b = min; b <= max; b++)
            {
                while (next[b] < ends[b])
                {
                    int i = next[b];
                    string? value = values[i];
                    int target = OrdinalDigit(value, index) + 2;
                    if (target == b)
                        next[b]++;
                    else
                    {
                        int destination = next[target]++;
                        values[i] = values[destination];
                        values[destination] = value;
                    }
                }
            }

            for (int b = Math.Max(2, min); b <= max; b++)
            {
                int start = b == min ? 0 : ends[b - 1], length = ends[b] - start;
                if (length > 1)
                    SortOrdinalBuckets(values.Slice(start, length), index + 1, budget);
            }
            return;
        }

        SortOrdinal(values, index, 64);
    }

    private static bool IsCultureOrdered(ReadOnlySpan<string?> values)
    {
        CompareInfo compare = CultureInfo.CurrentCulture.CompareInfo;
        for (int i = 1; i < values.Length; i++)
            if (compare.Compare(values[i - 1], values[i], CompareOptions.None) > 0)
                return false;
        return true;
    }

    private static bool TryOrdered<T>(Span<T> values, IComparer<T> comparer)
    {
        int i = 1, order = 0;
        while (i < values.Length && (order = comparer.Compare(values[i - 1], values[i])) == 0)
            i++;

        if (i == values.Length)
            return true;

        bool descending = order > 0;
        while (++i < values.Length)
        {
            order = comparer.Compare(values[i - 1], values[i]);
            if (descending ? order < 0 : order > 0)
                return false;
        }

        if (descending)
            values.Reverse();

        return true;
    }

    private static void SortDouble(Span<double> values)
    {
        if (TryOrdered(values, Comparer<double>.Default))
            return;

        if (values.Length <= 32)
        {
            InsertionSort(values, Comparer<double>.Default);
            return;
        }

        // NaNs sort first under Double.CompareTo. Keep every original payload.
        int nanCount = 0;
        if (ContainsNaN(values))
            for (int i = 0; i < values.Length; i++)
                if (double.IsNaN(values[i]))
                    Swap(values, nanCount++, i);

        Span<double> numbers = values[nanCount..];
        if (numbers.Length is >= 512 and <= 16384 &&
            (!Vector512.IsHardwareAccelerated || numbers.Length > 1024) &&
            (TrySortCompactDouble(numbers) || TrySortProjected(numbers)))
            return;

        Span<ulong> bits = MemoryMarshal.Cast<double, ulong>(numbers);
        if (bits.Length < 2)
            return;

        TransformDoubleKeys(bits, false);
        try
        {
            SortUInt64(bits);
        }
        finally
        {
            TransformDoubleKeys(bits, true);
        }
    }

    private static bool ContainsNaN(ReadOnlySpan<double> values)
    {
        ref double data = ref MemoryMarshal.GetReference(values);
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var magnitude = Vector512.Create(0x7fff_ffff_ffff_ffffUL);
            var infinity = Vector512.Create(0x7ff0_0000_0000_0000UL);
            for (; i <= values.Length - 8; i += 8)
            {
                var bits = Vector512.LoadUnsafe(ref data, (nuint)i).AsUInt64();
                if (Vector512.GreaterThanAny(bits & magnitude, infinity))
                    return true;
            }
        }
        for (; i < values.Length; i++)
            if (double.IsNaN(Unsafe.Add(ref data, i))) return true;
        return false;
    }

    // Monotone 32-bit projected keys: high bits identify a numeric interval,
    // low bits identify the original value. Resolve collisions exactly.
    private static bool TrySortCompactDouble(Span<double> values)
    {
        int length = values.Length;
        int indexBits = BitOperations.Log2((uint)(length - 1)) + 1;
        uint indexMask = (1u << indexBits) - 1;
        int limit = (int)(uint.MaxValue >> indexBits);
        ref double input = ref MemoryMarshal.GetReference(values);
        double min = input, max = input;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var lo = Vector512.Create(min);
            var hi = lo;
            for (; i <= length - 8; i += 8)
            {
                var v = Vector512.LoadUnsafe(ref input, (nuint)i);
                lo = Vector512.Min(lo, v);
                hi = Vector512.Max(hi, v);
            }
            for (int lane = 0; lane < 8; lane++)
            {
                min = Math.Min(min, lo.GetElement(lane));
                max = Math.Max(max, hi.GetElement(lane));
            }
        }
        for (; i < length; i++)
        {
            double value = Unsafe.Add(ref input, i);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
        double range = max - min;
        double scale = limit / range;
        if (!(range > 0) || !double.IsFinite(range) || !double.IsFinite(scale) || scale <= 0)
            return false;

        Span<int> keys = stackalloc int[length];
        ref int key = ref MemoryMarshal.GetReference(keys);
        i = 0;
        if (Avx512F.IsSupported && Avx512DQ.IsSupported)
        {
            var origin = Vector512.Create(min);
            var factor = Vector512.Create(scale);
            var ceiling = Vector512.Create((double)limit);
            var offsets = Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
            var sign = Vector512.Create(int.MinValue);
            for (; i <= length - 16; i += 16)
            {
                var a = Vector512.Min((Vector512.LoadUnsafe(ref input, (nuint)i) - origin) * factor, ceiling);
                var b = Vector512.Min((Vector512.LoadUnsafe(ref input, (nuint)(i + 8)) - origin) * factor, ceiling);
                var tags = Vector512.Narrow(Vector512.ConvertToInt64(a), Vector512.ConvertToInt64(b));
                var packed = ((tags << indexBits) | (offsets + Vector512.Create(i))) ^ sign;
                packed.StoreUnsafe(ref key, (nuint)i);
            }
        }
        for (; i < length; i++)
        {
            uint tag = (uint)Math.Min(limit, (int)((Unsafe.Add(ref input, i) - min) * scale));
            Unsafe.Add(ref key, i) = (int)(((tag << indexBits) | (uint)i) ^ 0x80000000u);
        }
        SortProjectedInt32(keys, indexBits);

        // Avoid large equal-prefix groups on clustered inputs.
        const int maxCollision = 32;
        for (i = maxCollision; i < length; i += maxCollision)
            if (((uint)(keys[i] ^ keys[i - maxCollision]) & ~indexMask) == 0)
                return false;

        double[]? rented = null;
        Span<double> original = length <= 8192 ? stackalloc double[length] :
            (rented = ArrayPool<double>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            values.CopyTo(original);
            ref double copy = ref MemoryMarshal.GetReference(original);
            for (i = 0; i < length; i++)
                Unsafe.Add(ref input, i) = Unsafe.Add(ref copy, (int)((uint)Unsafe.Add(ref key, i) & indexMask));

            for (int next = 1; next < length; next++)
            {
                next = NextProjectedCollision(ref key, next, length, indexMask);
                if (next == length) break;
                int start = next - 1;
                int end = next + 1;
                uint prefix = (uint)Unsafe.Add(ref key, start) & ~indexMask;
                while (end < length && ((uint)Unsafe.Add(ref key, end) & ~indexMask) == prefix)
                    end++;
                for (i = next; i < end; i++)
                {
                    double value = Unsafe.Add(ref input, i);
                    int j = i;
                    while (j > start && Unsafe.Add(ref input, j - 1) > value)
                    {
                        Unsafe.Add(ref input, j) = Unsafe.Add(ref input, j - 1);
                        j--;
                    }
                    Unsafe.Add(ref input, j) = value;
                }
                next = end;
            }
            return true;
        }
        finally { if (rented is not null) ArrayPool<double>.Shared.Return(rented); }
    }

    // Monotone numeric projection: a single scatter produces ordered buckets.
    // The input is left untouched if the range or occupancy makes this unsuitable.
    private static bool TrySortProjected(Span<double> values)
    {
        int length = values.Length;
        int buckets = 1 << Math.Clamp(BitOperations.Log2((uint)length) - 1, 5, 12);
        double min = values[0], max = min;
        ref double input = ref MemoryMarshal.GetReference(values);
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var lower = Vector512.Create(min);
            var upper = lower;
            for (; i <= length - 8; i += 8)
            {
                var v = Vector512.LoadUnsafe(ref input, (nuint)i);
                lower = Vector512.Min(lower, v);
                upper = Vector512.Max(upper, v);
            }
            for (int lane = 0; lane < 8; lane++)
            {
                min = Math.Min(min, lower.GetElement(lane));
                max = Math.Max(max, upper.GetElement(lane));
            }
        }
        for (; i < length; i++)
        {
            double x = Unsafe.Add(ref input, i);
            min = Math.Min(min, x);
            max = Math.Max(max, x);
        }

        double range = max - min;
        double scale = (buckets - 1) / range;
        if (!(range > 0) || !double.IsFinite(range) || !double.IsFinite(scale) || scale <= 0)
            return false;

        Span<int> counts = stackalloc int[buckets];
        counts.Clear();
        ref int histogram = ref MemoryMarshal.GetReference(counts);
        int limit = buckets - 1;
        Span<int> tags = stackalloc int[length];
        ref int tag = ref MemoryMarshal.GetReference(tags);
        i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var origin = Vector512.Create(min);
            var multiplier = Vector512.Create(scale);
            var ceiling = Vector512.Create((double)limit);
            Span<long> lanes = stackalloc long[8];
            ref long laneData = ref MemoryMarshal.GetReference(lanes);
            for (; i <= length - 8; i += 8)
            {
                var v = Vector512.LoadUnsafe(ref input, (nuint)i);
                Vector512.ConvertToInt64(Vector512.Min((v - origin) * multiplier, ceiling))
                    .StoreUnsafe(ref laneData);
                for (int j = 0; j < 8; j++)
                {
                    int bucket = (int)Unsafe.Add(ref laneData, j);
                    Unsafe.Add(ref tag, i + j) = bucket;
                    Unsafe.Add(ref histogram, bucket)++;
                }
            }
        }
        for (; i < length; i++)
        {
            int bucket = Math.Min(limit, (int)((Unsafe.Add(ref input, i) - min) * scale));
            Unsafe.Add(ref tag, i) = bucket;
            Unsafe.Add(ref histogram, bucket)++;
        }
        for (int b = 0; b < buckets; b++)
            if (Unsafe.Add(ref histogram, b) > 48)
                return false;

        int sum = 0;
        for (int b = 0; b < buckets; b++)
        {
            int count = Unsafe.Add(ref histogram, b);
            Unsafe.Add(ref histogram, b) = sum;
            sum += count;
        }

        double[]? rented = null;
        Span<double> scratch = length <= 8192 ? stackalloc double[length] :
            (rented = ArrayPool<double>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            ref double output = ref MemoryMarshal.GetReference(scratch);
            for (i = 0; i < length; i++)
            {
                double x = Unsafe.Add(ref input, i);
                int bucket = Unsafe.Add(ref tag, i);
                Unsafe.Add(ref output, Unsafe.Add(ref histogram, bucket)++) = x;
            }

            int start = 0;
            for (int b = 0; b < buckets; b++)
            {
                int end = Unsafe.Add(ref histogram, b);
                int size = end - start;
                if (size == 0)
                    continue;

                double first = Unsafe.Add(ref output, start);
                Unsafe.Add(ref input, start) = first;
                if (size == 2)
                {
                    double second = Unsafe.Add(ref output, start + 1);
                    bool swap = first > second;
                    Unsafe.Add(ref input, start) = swap ? second : first;
                    Unsafe.Add(ref input, start + 1) = swap ? first : second;
                }
                else
                {
                    for (i = start + 1; i < end; i++)
                    {
                        double x = Unsafe.Add(ref output, i);
                        int j = i;
                        while (j > start && Unsafe.Add(ref input, j - 1) > x)
                        {
                            Unsafe.Add(ref input, j) = Unsafe.Add(ref input, j - 1);
                            j--;
                        }
                        Unsafe.Add(ref input, j) = x;
                    }
                }
                start = end;
            }
            return true;
        }
        finally
        {
            if (rented is not null) ArrayPool<double>.Shared.Return(rented);
        }
    }

    private static void TransformDoubleKeys(Span<ulong> bits, bool inverse)
    {
        const ulong sign = 0x8000_0000_0000_0000UL;
        ref ulong data = ref MemoryMarshal.GetReference(bits);
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var signVector = Vector512.Create(sign);
            for (; i <= bits.Length - 8; i += 8)
            {
                var value = Vector512.LoadUnsafe(ref data, (nuint)i);
                var mask = (value.AsInt64() >> 63).AsUInt64();
                if (inverse) mask = ~mask;
                (value ^ (mask | signVector)).StoreUnsafe(ref data, (nuint)i);
            }
        }

        for (; i < bits.Length; i++)
        {
            ref ulong value = ref Unsafe.Add(ref data, i);
            ulong mask = (ulong)((long)value >> 63);
            value ^= (inverse ? ~mask : mask) | sign;
        }
    }

    private static void SortUInt64Wide(Span<ulong> bits)
    {
        const int width = 11, stride = 2048;
        Span<int> counts = stackalloc int[stride * 5 + 512];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        ref ulong data = ref MemoryMarshal.GetReference(bits);
        for (int i = 0; i < bits.Length; i++)
        {
            ulong key = Unsafe.Add(ref data, i);
            Unsafe.Add(ref h, (int)(key & 2047))++;
            Unsafe.Add(ref h, stride + (int)((key >> 11) & 2047))++;
            Unsafe.Add(ref h, stride * 2 + (int)((key >> 22) & 2047))++;
            Unsafe.Add(ref h, stride * 3 + (int)((key >> 33) & 2047))++;
            Unsafe.Add(ref h, stride * 4 + (int)((key >> 44) & 2047))++;
            Unsafe.Add(ref h, stride * 5 + (int)(key >> 55))++;
        }

        ulong[]? rented = null;
        Span<ulong> scratch = bits.Length <= 8192 ? stackalloc ulong[bits.Length] :
            (rented = ArrayPool<ulong>.Shared.Rent(bits.Length)).AsSpan(0, bits.Length);
        try
        {
            bool inScratch = false;
            ulong first = bits[0];
            for (int digit = 0; digit < 6; digit++)
            {
                int shift = digit * width;
                Span<int> histogram = counts.Slice(digit * stride, digit == 5 ? 512 : stride);
                ulong mask = (uint)(histogram.Length - 1);
                if (histogram[(int)((first >> shift) & mask)] == bits.Length)
                    continue;
                int sum = 0;
                for (int b = 0; b < histogram.Length; b++)
                {
                    int frequency = histogram[b];
                    histogram[b] = sum;
                    sum += frequency;
                }
                if (inScratch)
                    ScatterUInt64Wide(scratch, bits, histogram, shift, mask);
                else
                    ScatterUInt64Wide(bits, scratch, histogram, shift, mask);
                inScratch = !inScratch;
            }
            if (inScratch) scratch.CopyTo(bits);
        }
        finally
        {
            if (rented is not null) ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ScatterUInt64Wide(ReadOnlySpan<ulong> source, Span<ulong> target,
        Span<int> histogram, int shift, ulong mask)
    {
        ref ulong input = ref MemoryMarshal.GetReference(source);
        ref ulong output = ref MemoryMarshal.GetReference(target);
        ref int offsets = ref MemoryMarshal.GetReference(histogram);
        for (int i = 0; i < source.Length; i++)
        {
            ulong value = Unsafe.Add(ref input, i);
            int bucket = (int)((value >> shift) & mask);
            Unsafe.Add(ref output, Unsafe.Add(ref offsets, bucket)++) = value;
        }
    }

    private static void SortUInt64(Span<ulong> bits, int firstDigit = 0)
    {
        if (bits.Length < 2)
            return;
        if (Vector512.IsHardwareAccelerated && bits.Length <= 1024)
        {
            SimdSort.SortUInt64(bits);
            return;
        }

        if (firstDigit == 0 && bits.Length >= 4096)
        {
            SortUInt64Wide(bits);
            return;
        }

        ulong[]? rented = null;
        Span<ulong> scratch = bits.Length <= 8192
            ? stackalloc ulong[bits.Length]
            : (rented = ArrayPool<ulong>.Shared.Rent(bits.Length)).AsSpan(0, bits.Length);

        try
        {
            Span<int> counts = stackalloc int[8 * 256];
            counts.Clear();
            ref int h = ref MemoryMarshal.GetReference(counts);
            ref ulong data = ref MemoryMarshal.GetReference(bits);
            for (int i = 0; i < bits.Length; i++)
            {
                ulong key = Unsafe.Add(ref data, i);
                Unsafe.Add(ref h, (byte)key)++;
                Unsafe.Add(ref h, 256 + (byte)(key >> 8))++;
                Unsafe.Add(ref h, 512 + (byte)(key >> 16))++;
                Unsafe.Add(ref h, 768 + (byte)(key >> 24))++;
                Unsafe.Add(ref h, 1024 + (byte)(key >> 32))++;
                Unsafe.Add(ref h, 1280 + (byte)(key >> 40))++;
                Unsafe.Add(ref h, 1536 + (byte)(key >> 48))++;
                Unsafe.Add(ref h, 1792 + (byte)(key >> 56))++;
            }

            bool inScratch = false;
            ulong first = bits[0];
            for (int digit = firstDigit; digit < 8; digit++)
            {
                int shift = digit * 8;
                Span<int> histogram = counts.Slice(digit * 256, 256);
                if (histogram[(byte)(first >> shift)] == bits.Length)
                    continue;

                int sum = 0;
                for (int b = 0; b < 256; b++)
                {
                    int frequency = histogram[b];
                    histogram[b] = sum;
                    sum += frequency;
                }

                if (inScratch)
                    ScatterUInt64(scratch, bits, histogram, shift);
                else
                    ScatterUInt64(bits, scratch, histogram, shift);
                inScratch = !inScratch;
            }

            if (inScratch)
                scratch.CopyTo(bits);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ScatterUInt64(ReadOnlySpan<ulong> source, Span<ulong> target,
        Span<int> histogram, int shift)
    {
        ref ulong input = ref MemoryMarshal.GetReference(source);
        ref ulong output = ref MemoryMarshal.GetReference(target);
        ref int offsets = ref MemoryMarshal.GetReference(histogram);
        for (int i = 0; i < source.Length; i++)
        {
            ulong value = Unsafe.Add(ref input, i);
            int bucket = (byte)(value >> shift);
            Unsafe.Add(ref output, Unsafe.Add(ref offsets, bucket)++) = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OrdinalDigit(string? value, int index) =>
        value is null ? -2 : index >= value.Length ? -1 : value[index];

    // Three-way multikey quicksort: equal prefixes are examined only once.
    // Use full ordinal comparisons at small partitions and at a bounded depth.
    private static void SortOrdinal(Span<string?> values, int index, int budget)
    {
        while (values.Length > InsertionThreshold && budget-- > 0)
        {
            int a = OrdinalDigit(values[0], index);
            int b = OrdinalDigit(values[values.Length >> 1], index);
            int c = OrdinalDigit(values[^1], index);
            int pivot = Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
            int lower = 0, current = 0, upper = values.Length - 1;
            while (current <= upper)
            {
                int key = OrdinalDigit(values[current], index);
                if (key < pivot)
                    Swap(values, lower++, current++);
                else if (key > pivot)
                    Swap(values, current, upper--);
                else
                    current++;
            }

            SortOrdinal(values[..lower], index, budget);
            SortOrdinal(values[(upper + 1)..], index, budget);
            if (pivot < 0)
                return;

            values = values[lower..(upper + 1)];
            index++;
        }

        if (values.Length > 1)
            IntroSort(values, new OrdinalComparer(), 2 * (BitOperations.Log2((uint)values.Length) + 1));
    }

    private readonly struct OrdinalComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);
    }

    private static void IntroSort<T, TComparer>(Span<T> values, TComparer comparer, int depth)
        where TComparer : IComparer<T>
    {
        while (values.Length > InsertionThreshold)
        {
            if (depth-- == 0)
            {
                HeapSort(values, comparer);
                return;
            }

            ref T data = ref MemoryMarshal.GetReference(values);
            int last = values.Length - 1;
            int middle = last >> 1;
            ref T firstValue = ref data;
            ref T middleValue = ref Unsafe.Add(ref data, middle);
            ref T lastValue = ref Unsafe.Add(ref data, last);

            if (comparer.Compare(firstValue, middleValue) > 0)
                (firstValue, middleValue) = (middleValue, firstValue);
            if (comparer.Compare(firstValue, lastValue) > 0)
                (firstValue, lastValue) = (lastValue, firstValue);
            if (comparer.Compare(middleValue, lastValue) > 0)
                (middleValue, lastValue) = (lastValue, middleValue);

            T pivot = middleValue;
            int lower = 1, upper = last - 1;
            while (true)
            {
                while (lower < last && comparer.Compare(Unsafe.Add(ref data, lower), pivot) < 0)
                    lower++;
                while (upper > 0 && comparer.Compare(Unsafe.Add(ref data, upper), pivot) > 0)
                    upper--;
                if (lower >= upper)
                    break;

                ref T a = ref Unsafe.Add(ref data, lower++);
                ref T b = ref Unsafe.Add(ref data, upper--);
                (a, b) = (b, a);
            }

            int split = Math.Clamp(upper + 1, 1, last);
            Span<T> left = values[..split];
            Span<T> right = values[split..];

            if (left.Length < right.Length)
            {
                IntroSort(left, comparer, depth);
                values = right;
            }
            else
            {
                IntroSort(right, comparer, depth);
                values = left;
            }
        }

        InsertionSort(values, comparer);
    }

    private static void InsertionSort<T, TComparer>(Span<T> values, TComparer comparer)
        where TComparer : IComparer<T>
    {
        ref T data = ref MemoryMarshal.GetReference(values);
        for (int i = 1; i < values.Length; i++)
        {
            T value = Unsafe.Add(ref data, i);
            int j = i;
            while (j > 0 && comparer.Compare(value, Unsafe.Add(ref data, j - 1)) < 0)
            {
                Unsafe.Add(ref data, j) = Unsafe.Add(ref data, j - 1);
                j--;
            }

            if (j != i)
                Unsafe.Add(ref data, j) = value;
        }
    }

    private static void HeapSort<T, TComparer>(Span<T> values, TComparer comparer)
        where TComparer : IComparer<T>
    {
        for (int i = values.Length / 2 - 1; i >= 0; i--)
            SiftDown(values, i, comparer);

        for (int end = values.Length - 1; end > 0; end--)
        {
            Swap(values, 0, end);
            SiftDown(values[..end], 0, comparer);
        }
    }

    private static void SiftDown<T, TComparer>(Span<T> values, int root, TComparer comparer)
        where TComparer : IComparer<T>
    {
        while (root < values.Length / 2)
        {
            int child = root * 2 + 1;
            if (child + 1 < values.Length &&
                comparer.Compare(values[child], values[child + 1]) < 0)
                child++;

            if (comparer.Compare(values[root], values[child]) >= 0)
                return;

            Swap(values, root, child);
            root = child;
        }
    }

    private static void Swap<T>(Span<T> values, int a, int b)
    {
        if (a != b)
            (values[a], values[b]) = (values[b], values[a]);
    }
}
