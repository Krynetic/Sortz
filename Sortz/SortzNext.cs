using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Buffers.Binary;
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
                SortOrdinalFast(strings);
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
            SortOrdinalFast(strings);
            // Ordinal order often matches culture-sensitive order for identifiers.
            // Always verify against the actual comparer, never assume equivalence.
            if (TryOrdered(strings, Comparer<string?>.Default))
                return;
            if (values.Length >= 2048 && TrySortCultureStrings(strings))
                return;
        }

        if (ReferenceEquals(comparer, Comparer<T>.Default) &&
            ComparableDispatch<T>.Sort is { } sort)
        {
            sort(values);
            return;
        }

        IntroSort(values, comparer, 2 * (BitOperations.Log2((uint)values.Length) + 1));
    }

    private delegate void SpanSorter<T>(Span<T> values);

    private static class ComparableDispatch<T>
    {
        internal static readonly SpanSorter<T>? Sort = Create();

        private static SpanSorter<T>? Create()
        {
            if (!typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
                return null;

            return typeof(SortzNext).GetMethod(nameof(SortComparable),
                    BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(T)).CreateDelegate<SpanSorter<T>>();
        }
    }

    private static void SortComparable<T>(Span<T> values) where T : IComparable<T> =>
        IntroSort(values, new DirectComparable<T>(), 2 * (BitOperations.Log2((uint)values.Length) + 1));

    private readonly struct DirectComparable<T> : IComparer<T> where T : IComparable<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T? x, T? y) =>
            x is null ? y is null ? 0 : -1 : y is null ? 1 : x.CompareTo(y);
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

        if (Vector512.IsHardwareAccelerated && count <= 256)
        {
            SimdSort.SortInt32(values);
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

    private readonly record struct OrdinalEntry(string? Value, ulong Key);
    private readonly struct OrdinalEntryComparer : IComparer<OrdinalEntry>
    {
        public int Compare(OrdinalEntry x, OrdinalEntry y) =>
            x.Key < y.Key ? -1 : x.Key > y.Key ? 1 : string.CompareOrdinal(x.Value, y.Value);
    }

    // Prefix keys preserve ordinal UTF-16 ordering. When all prefixes
    // fit Latin-1, pack twice as many code units into each key.
    private static void SortOrdinalFast(Span<string?> values)
    {
        if (values.Length < 64)
        {
            SortOrdinal(values, 0, 64);
            return;
        }

        bool latin1 = true;
        foreach (string? value in values)
        {
            if (value is null)
                continue;
            int count = Math.Min(8, value.Length);
            for (int j = 0; j < count; j++)
                if (value[j] > 255)
                {
                    latin1 = false;
                    break;
                }
            if (!latin1)
                break;
        }

        OrdinalEntry[] rented = ArrayPool<OrdinalEntry>.Shared.Rent(values.Length);
        Span<OrdinalEntry> entries = rented.AsSpan(0, values.Length);
        try
        {
            for (int i = 0; i < values.Length; i++)
                entries[i] = new OrdinalEntry(values[i], OrdinalPrefix(values[i], latin1));

            IntroSort(entries, new OrdinalEntryComparer(),
                2 * (BitOperations.Log2((uint)entries.Length) + 1));

            for (int i = 0; i < values.Length; i++)
                values[i] = entries[i].Value;
        }
        finally
        {
            entries.Clear();
            ArrayPool<OrdinalEntry>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong OrdinalPrefix(string? value, bool latin1)
    {
        if (value is null)
            return 0;

        if (latin1 && value.Length >= 8 && Bmi2.X64.IsSupported && BitConverter.IsLittleEndian)
        {
            ref byte first = ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value.AsSpan()));
            ulong a = Unsafe.ReadUnaligned<ulong>(ref first);
            ulong b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref first, 8));
            const ulong mask = 0x00ff00ff00ff00ffUL;
            ulong packed = Bmi2.X64.ParallelBitExtract(a, mask) |
                (Bmi2.X64.ParallelBitExtract(b, mask) << 32);
            return BinaryPrimitives.ReverseEndianness(packed);
        }

        int width = latin1 ? 8 : 16;
        int count = Math.Min(64 / width, value.Length);
        ulong key = 0;
        for (int i = 0; i < count; i++)
            key |= (ulong)value[i] << (64 - width * (i + 1));
        return key;
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

        if (Vector512.IsHardwareAccelerated && bits.Length <= 256)
        {
            foreach (ref ulong item in bits)
                item ^= (ulong)((long)item >> 63) | 0x8000_0000_0000_0000UL;

            SimdSort.SortUInt64(bits);

            foreach (ref ulong item in bits)
                item ^= (ulong)(((long)item >> 63) ^ -1L) | 0x8000_0000_0000_0000UL;
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
            // Transform once, rather than repeating the IEEE-754 mapping
            // for each radix pass. The mapping is reversible, including +/-0.
            for (int i = 0; i < bits.Length; i++)
            {
                ref ulong item = ref Unsafe.Add(ref data, i);
                ulong key = item ^ ((ulong)((long)item >> 63) | 0x8000_0000_0000_0000UL);
                item = key;
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
            for (int digit = 0; digit < 8; digit++)
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
