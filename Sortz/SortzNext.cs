using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sortz;

public static class SortzNext
{
    private const int InsertionThreshold = 16;

    extension<T>(List<T> list)
    {
        public void SortNext(IComparer<T>? comparer = null)
        {
            ArgumentNullException.ThrowIfNull(list);
            Sort(CollectionsMarshal.AsSpan(list), comparer);
        }
    }

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

        if (typeof(T) == typeof(string) && ReferenceEquals(comparer, StringComparer.Ordinal))
        {
            var strings = MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, string?>(ref MemoryMarshal.GetReference(values)), values.Length);
            if (!TryOrdered(strings, StringComparer.Ordinal))
                SortOrdinalBuckets(strings, 0, 32);
            return;
        }

        comparer ??= Comparer<T>.Default;
        if (TryOrdered(values, comparer))
            return;

        if (typeof(T) == typeof(string) && values.Length >= 2048 &&
            ReferenceEquals(comparer, Comparer<T>.Default) &&
            TrySortCultureStrings(MemoryMarshal.CreateSpan(
                ref Unsafe.As<T, string?>(ref MemoryMarshal.GetReference(values)), values.Length)))
            return;

        IntroSort(values, comparer, 2 * (BitOperations.Log2((uint)values.Length) + 1));
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

        uint[]? rented = null;
        Span<uint> bits = MemoryMarshal.Cast<int, uint>(values);
        Span<uint> scratch = count <= 16384
            ? stackalloc uint[count]
            : (rented = ArrayPool<uint>.Shared.Rent(count)).AsSpan(0, count);
        try
        {
            for (int j = 0; j < count; j++)
                bits[j] ^= 0x8000_0000u;

            RadixMsd(bits, scratch);

            for (int j = 0; j < count; j++)
                bits[j] ^= 0x8000_0000u;
        }
        finally
        {
            if (rented is not null)
                ArrayPool<uint>.Shared.Return(rented);
        }
    }

    // Adaptive most-significant-digit distribution. Small buckets are
    // finished locally, rather than traversing every element for every byte.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void RadixMsd<T>(Span<T> values, Span<T> scratch)
        where T : unmanaged, IBinaryInteger<T>, IUnsignedNumber<T>
    {
        int length = values.Length;
        if (length <= 24)
        {
            for (int i = 1; i < length; i++)
            {
                T key = values[i];
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

        ref T input = ref MemoryMarshal.GetReference(values);
        T first = input;
        T varying = T.Zero;
        for (int i = 1; i < length; i++)
            varying |= Unsafe.Add(ref input, i) ^ first;
        ulong mask = ulong.CreateTruncating(varying);
        if (mask == 0)
            return;

        int significant = BitOperations.Log2(mask) + 1;
        int width = Math.Min(significant,
            Math.Clamp(BitOperations.Log2((uint)length) - 1, 4, 11));
        int shift = significant - width;
        int bucketCount = 1 << width;
        uint bucketMask = (uint)(bucketCount - 1);
        Span<int> offsets = stackalloc int[bucketCount];
        offsets.Clear();
        ref int offset = ref MemoryMarshal.GetReference(offsets);

        for (int i = 0; i < length; i++)
        {
            int bucket = (int)((ulong.CreateTruncating(Unsafe.Add(ref input, i)) >> shift) & bucketMask);
            Unsafe.Add(ref offset, bucket)++;
        }

        int sum = 0;
        for (int b = 0; b < bucketCount; b++)
        {
            int frequency = Unsafe.Add(ref offset, b);
            Unsafe.Add(ref offset, b) = sum;
            sum += frequency;
        }

        ref T output = ref MemoryMarshal.GetReference(scratch);
        for (int i = 0; i < length; i++)
        {
            T value = Unsafe.Add(ref input, i);
            int bucket = (int)((ulong.CreateTruncating(value) >> shift) & bucketMask);
            Unsafe.Add(ref output, Unsafe.Add(ref offset, bucket)++) = value;
        }

        if (shift != 0)
        {
            int begin = 0;
            for (int b = 0; b < bucketCount; b++)
            {
                int end = Unsafe.Add(ref offset, b);
                int size = end - begin;
                if (size > 1)
                    RadixMsd(scratch.Slice(begin, size), values.Slice(begin, size));
                begin = end;
            }
        }

        scratch.CopyTo(values);
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

    // In-place MSD bucket distribution for Latin-1 code units, with a
    // general UTF-16 multikey fallback. Null and end-of-string sort first.
    private static void SortOrdinalBuckets(Span<string?> values, int index, int budget)
    {
        if (values.Length < 128 || budget == 0)
        {
            SortOrdinal(values, index, 64);
            return;
        }

        Span<int> ends = stackalloc int[258];
        Span<int> next = stackalloc int[258];
        while (budget-- > 0)
        {
            ends.Clear();
            int nonEmpty = 0, only = 0;
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
            for (int b = 0; b < ends.Length; b++)
            {
                next[b] = sum;
                sum += ends[b];
                ends[b] = sum;
            }

            for (int b = 0; b < ends.Length; b++)
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

            for (int b = 2; b < ends.Length; b++)
            {
                int start = ends[b - 1], length = ends[b] - start;
                if (length > 1)
                    SortOrdinalBuckets(values.Slice(start, length), index + 1, budget);
            }
            return;
        }

        SortOrdinal(values, index, 64);
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
        for (int i = 0; i < values.Length; i++)
            if (double.IsNaN(values[i]))
                Swap(values, nanCount++, i);

        Span<ulong> bits = MemoryMarshal.Cast<double, ulong>(values[nanCount..]);
        if (bits.Length < 2)
            return;

        ulong[]? rented = null;
        Span<ulong> scratch = bits.Length <= 8192
            ? stackalloc ulong[bits.Length]
            : (rented = ArrayPool<ulong>.Shared.Rent(bits.Length)).AsSpan(0, bits.Length);

        try
        {
            ref ulong data = ref MemoryMarshal.GetReference(bits);
            for (int i = 0; i < bits.Length; i++)
            {
                ref ulong item = ref Unsafe.Add(ref data, i);
                item ^= (ulong)((long)item >> 63) | 0x8000_0000_0000_0000UL;
            }

            RadixMsd(bits, scratch);

            for (int i = 0; i < bits.Length; i++)
            {
                ref ulong item = ref Unsafe.Add(ref data, i);
                item ^= (ulong)(((long)item >> 63) ^ -1L) | 0x8000_0000_0000_0000UL;
            }
        }
        finally
        {
            if (rented is not null)
                ArrayPool<ulong>.Shared.Return(rented);
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
            IntroSort(values, StringComparer.Ordinal, 2 * (BitOperations.Log2((uint)values.Length) + 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

            int last = values.Length - 1;
            int middle = last >> 1;

            if (comparer.Compare(values[0], values[middle]) > 0)
                Swap(values, 0, middle);
            if (comparer.Compare(values[0], values[last]) > 0)
                Swap(values, 0, last);
            if (comparer.Compare(values[middle], values[last]) > 0)
                Swap(values, middle, last);

            T pivot = values[middle];
            int lower = 0, upper = last;

            // Median-of-three provides sentinels for both scans.
            while (true)
            {
                while (comparer.Compare(values[lower], pivot) < 0)
                    lower++;
                while (comparer.Compare(values[upper], pivot) > 0)
                    upper--;

                if (lower >= upper)
                    break;

                Swap(values, lower++, upper--);
            }

            int split = upper + 1;
            if (split == values.Length)
                split--;
            if (split == 0)
                split++;

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
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            int j = i;
            while (j > 0 && comparer.Compare(value, values[j - 1]) < 0)
            {
                values[j] = values[j - 1];
                j--;
            }

            if (j != i)
                values[j] = value;
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
