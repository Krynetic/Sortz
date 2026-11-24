using System.Buffers;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Sortz;

public static class SortzNext
{
    private const int InsertionThreshold = 24;

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
                SortOrdinal(strings, 0, 64);
            return;
        }

        comparer ??= Comparer<T>.Default;
        if (TryOrdered(values, comparer))
            return;

        if (typeof(T) == typeof(string) && values.Length >= 1024 &&
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

    private readonly record struct CultureEntry(string? Value, int Offset, int Length);

    private sealed class CultureEntryComparer(byte[] keys) : IComparer<CultureEntry>
    {
        public int Compare(CultureEntry x, CultureEntry y)
        {
            if (x.Value is null)
                return y.Value is null ? 0 : -1;
            if (y.Value is null)
                return 1;

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
            int total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string? value = values[i];
                int length = value is null ? 0 : compareInfo.GetSortKeyLength(value.AsSpan(), CompareOptions.None);
                if (length > MaxKeyBytes - total)
                    return false;
                entries[i] = new CultureEntry(value, total, length);
                total += length;
            }

            keys = ArrayPool<byte>.Shared.Rent(total);
            for (int i = 0; i < entries.Length; i++)
            {
                CultureEntry entry = entries[i];
                if (entry.Value is not null)
                    compareInfo.GetSortKey(entry.Value.AsSpan(), keys.AsSpan(entry.Offset, entry.Length), CompareOptions.None);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong DoubleKey(double value)
    {
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        ulong magnitude = bits & 0x7fff_ffff_ffff_ffffUL;
        // Double.CompareTo orders every NaN before all numbers and treats +/-0 as equal.
        if (magnitude > 0x7ff0_0000_0000_0000UL)
            return 0;
        if (magnitude == 0)
            return 0x8000_0000_0000_0000UL;

        return bits ^ ((ulong)((long)bits >> 63) | 0x8000_0000_0000_0000UL);
    }

    private static void SortDouble(Span<double> values)
    {
        if (TryOrdered(values, Comparer<double>.Default))
            return;

        if (values.Length <= 64)
        {
            InsertionSort(values, Comparer<double>.Default);
            return;
        }

        Span<int> counts = stackalloc int[8 * 256];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        foreach (double value in values)
        {
            ulong key = DoubleKey(value);
            Unsafe.Add(ref h, (byte)key)++;
            Unsafe.Add(ref h, 256 + (byte)(key >> 8))++;
            Unsafe.Add(ref h, 512 + (byte)(key >> 16))++;
            Unsafe.Add(ref h, 768 + (byte)(key >> 24))++;
            Unsafe.Add(ref h, 1024 + (byte)(key >> 32))++;
            Unsafe.Add(ref h, 1280 + (byte)(key >> 40))++;
            Unsafe.Add(ref h, 1536 + (byte)(key >> 48))++;
            Unsafe.Add(ref h, 1792 + (byte)(key >> 56))++;
        }

        double[]? rented = null;
        Span<double> scratch = values.Length <= 8192
            ? stackalloc double[values.Length]
            : (rented = ArrayPool<double>.Shared.Rent(values.Length)).AsSpan(0, values.Length);

        try
        {
            bool inScratch = false;
            ulong first = DoubleKey(values[0]);
            for (int digit = 0; digit < 8; digit++)
            {
                int shift = digit * 8;
                Span<int> histogram = counts.Slice(digit * 256, 256);
                if (histogram[(byte)(first >> shift)] == values.Length)
                    continue;

                int sum = 0;
                for (int b = 0; b < 256; b++)
                {
                    int frequency = histogram[b];
                    histogram[b] = sum;
                    sum += frequency;
                }

                if (inScratch)
                    ScatterDouble(scratch, values, histogram, shift);
                else
                    ScatterDouble(values, scratch, histogram, shift);
                inScratch = !inScratch;
            }

            if (inScratch)
                scratch.CopyTo(values);
        }
        finally
        {
            if (rented is not null)
                ArrayPool<double>.Shared.Return(rented);
        }
    }

    private static void ScatterDouble(ReadOnlySpan<double> source, Span<double> target,
        Span<int> histogram, int shift)
    {
        ref double input = ref MemoryMarshal.GetReference(source);
        ref double output = ref MemoryMarshal.GetReference(target);
        ref int offsets = ref MemoryMarshal.GetReference(histogram);
        for (int i = 0; i < source.Length; i++)
        {
            double value = Unsafe.Add(ref input, i);
            int bucket = (byte)(DoubleKey(value) >> shift);
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
            IntroSort(values, StringComparer.Ordinal, 2 * (BitOperations.Log2((uint)values.Length) + 1));
    }

    private static void IntroSort<T>(Span<T> values, IComparer<T> comparer, int depth)
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

    private static void InsertionSort<T>(Span<T> values, IComparer<T> comparer)
    {
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            int j = i;
            while (j > 0 && comparer.Compare(value, values[j - 1]) < 0)
                j--;

            if (j == i)
                continue;

            values[j..i].CopyTo(values[(j + 1)..(i + 1)]);
            values[j] = value;
        }
    }

    private static void HeapSort<T>(Span<T> values, IComparer<T> comparer)
    {
        for (int i = values.Length / 2 - 1; i >= 0; i--)
            SiftDown(values, i, comparer);

        for (int end = values.Length - 1; end > 0; end--)
        {
            Swap(values, 0, end);
            SiftDown(values[..end], 0, comparer);
        }
    }

    private static void SiftDown<T>(Span<T> values, int root, IComparer<T> comparer)
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
