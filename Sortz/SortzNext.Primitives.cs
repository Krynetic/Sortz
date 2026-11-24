using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Sortz;

public static partial class SortzNext
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Span<TTo> Reinterpret<TFrom, TTo>(Span<TFrom> values) =>
        MemoryMarshal.CreateSpan(ref Unsafe.As<TFrom, TTo>(ref MemoryMarshal.GetReference(values)), values.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TrySortPrimitive<T>(Span<T> values)
    {
        if (typeof(T) == typeof(bool)) { SortBoolean(Reinterpret<T, bool>(values)); return true; }
        if (typeof(T) == typeof(byte)) { SortByte(Reinterpret<T, byte>(values)); return true; }
        if (typeof(T) == typeof(sbyte)) { SortSByte(Reinterpret<T, sbyte>(values)); return true; }
        if (typeof(T) == typeof(char)) { SortChar(Reinterpret<T, char>(values)); return true; }
        if (typeof(T) == typeof(ushort)) { SortUInt16(Reinterpret<T, ushort>(values)); return true; }
        if (typeof(T) == typeof(short)) { SortInt16(Reinterpret<T, short>(values)); return true; }
        if (typeof(T) == typeof(uint)) { SortUInt32(Reinterpret<T, uint>(values)); return true; }
        if (typeof(T) == typeof(long)) { SortInt64(Reinterpret<T, long>(values)); return true; }
        if (typeof(T) == typeof(ulong)) { SortUnsigned64(Reinterpret<T, ulong>(values)); return true; }
        if (typeof(T) == typeof(nint)) { SortNativeInt(Reinterpret<T, nint>(values)); return true; }
        if (typeof(T) == typeof(nuint)) { SortNativeUInt(Reinterpret<T, nuint>(values)); return true; }
        if (typeof(T) == typeof(float)) { SortSingle(Reinterpret<T, float>(values)); return true; }
        if (typeof(T) == typeof(Half)) { SortHalf(Reinterpret<T, Half>(values)); return true; }
        if (typeof(T) == typeof(Int128)) { SortInt128(Reinterpret<T, Int128>(values)); return true; }
        if (typeof(T) == typeof(UInt128)) { SortUnsigned128(Reinterpret<T, UInt128>(values)); return true; }
        if (typeof(T) == typeof(decimal)) { SortDecimal(Reinterpret<T, decimal>(values)); return true; }
        return false;
    }

    private static void SortBoolean(Span<bool> values)
    {
        Span<byte> bytes = MemoryMarshal.AsBytes(values);
        int count = 0, i = 0;
        ref byte data = ref MemoryMarshal.GetReference(bytes);
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= bytes.Length - 64; i += 64)
                count += BitOperations.PopCount(Vector512.ExtractMostSignificantBits(
                    Vector512.Equals(Vector512.LoadUnsafe(ref data, (nuint)i), Vector512<byte>.Zero)));
        }
        for (; i < bytes.Length; i++)
            count += Unsafe.Add(ref data, i) == 0 ? 1 : 0;
        values[..count].Clear();
        values[count..].Fill(true);
    }

    private static void SortByte(Span<byte> values) => SortByteCore(values, 0);
    private static void SortSByte(Span<sbyte> values) => SortByteCore(MemoryMarshal.Cast<sbyte, byte>(values), 128);

    private static void SortByteCore(Span<byte> values, int sign)
    {
        if (values.Length <= 16)
        {
            for (int i = 1; i < values.Length; i++)
            {
                byte key = values[i];
                int j = i;
                while (j > 0 && (values[j - 1] ^ sign) > (key ^ sign))
                {
                    values[j] = values[j - 1];
                    j--;
                }
                values[j] = key;
            }
            return;
        }

        Span<int> counts = stackalloc int[256];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        foreach (byte value in values)
            Unsafe.Add(ref h, value ^ sign)++;
        int offset = 0;
        for (int i = 0; i < 256; i++)
        {
            int frequency = Unsafe.Add(ref h, i);
            if (frequency == 0) continue;
            values.Slice(offset, frequency).Fill((byte)(i ^ sign));
            offset += frequency;
        }
    }

    private static void SortChar(Span<char> values) => SortUInt16(MemoryMarshal.Cast<char, ushort>(values));

    private static void SortInt16(Span<short> values)
    {
        if (TryOrderedInteger(values)) return;
        var bits = MemoryMarshal.Cast<short, ushort>(values);
        XorKeys(bits, (ushort)0x8000);
        try { SortUInt16(bits); }
        finally { XorKeys(bits, (ushort)0x8000); }
    }

    private static void SortUInt16(Span<ushort> values)
    {
        if (TryOrderedInteger(values)) return;
        int length = values.Length;
        if (length <= 32) { InsertionInteger(values); return; }
        if (Vector512.IsHardwareAccelerated && length <= 1024)
        {
            Span<int> widened = stackalloc int[length];
            for (int i = 0; i < length; i++) widened[i] = values[i];
            SimdSort.SortInt32(widened);
            for (int i = 0; i < length; i++) values[i] = (ushort)widened[i];
            return;
        }

        Span<int> counts = stackalloc int[512];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        foreach (ushort value in values)
        {
            Unsafe.Add(ref h, (byte)value)++;
            Unsafe.Add(ref h, 256 + (value >> 8))++;
        }
        ushort first = values[0];
        bool lowUniform = counts[(byte)first] == length;
        bool highUniform = counts[256 + (first >> 8)] == length;
        if (lowUniform || highUniform)
        {
            if (lowUniform && highUniform) return;
            int offset = 0;
            for (int b = 0; b < 256; b++)
            {
                int frequency = counts[(lowUniform ? 256 : 0) + b];
                if (frequency == 0) continue;
                ushort value = lowUniform ? (ushort)((b << 8) | (byte)first) : (ushort)((first & 0xff00) | b);
                values.Slice(offset, frequency).Fill(value);
                offset += frequency;
            }
            return;
        }

        PrefixPrimitive(counts[..256]);
        PrefixPrimitive(counts[256..]);
        ushort[]? rented = null;
        Span<ushort> scratch = length <= 16384 ? stackalloc ushort[length] :
            (rented = ArrayPool<ushort>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            ref ushort source = ref MemoryMarshal.GetReference(values);
            ref ushort target = ref MemoryMarshal.GetReference(scratch);
            for (int i = 0; i < length; i++)
            {
                ushort value = Unsafe.Add(ref source, i);
                Unsafe.Add(ref target, Unsafe.Add(ref h, (byte)value)++) = value;
            }
            for (int i = 0; i < length; i++)
            {
                ushort value = Unsafe.Add(ref target, i);
                Unsafe.Add(ref source, Unsafe.Add(ref h, 256 + (value >> 8))++) = value;
            }
        }
        finally { if (rented is not null) ArrayPool<ushort>.Shared.Return(rented); }
    }

    private static void SortUInt32(Span<uint> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        XorKeys(values, 0x80000000u);
        try { SortInt32(MemoryMarshal.Cast<uint, int>(values)); }
        finally { XorKeys(values, 0x80000000u); }
    }

    private static void SortInt64(Span<long> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        var bits = MemoryMarshal.Cast<long, ulong>(values);
        XorKeys(bits, 0x8000000000000000UL);
        try { SortUInt64(bits); }
        finally { XorKeys(bits, 0x8000000000000000UL); }
    }

    private static void SortUnsigned64(Span<ulong> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        SortUInt64(values);
    }

    private static void SortNativeInt(Span<nint> values)
    {
        if (IntPtr.Size == 8) SortInt64(MemoryMarshal.Cast<nint, long>(values));
        else SortInt32(MemoryMarshal.Cast<nint, int>(values));
    }

    private static void SortNativeUInt(Span<nuint> values)
    {
        if (IntPtr.Size == 8) SortUnsigned64(MemoryMarshal.Cast<nuint, ulong>(values));
        else SortUInt32(MemoryMarshal.Cast<nuint, uint>(values));
    }

    private static void SortSingle(Span<float> values)
    {
        if (TryOrdered(values, Comparer<float>.Default)) return;
        if (values.Length <= 32) { InsertionSort(values, Comparer<float>.Default); return; }
        int first = 0;
        for (int i = 0; i < values.Length; i++)
            if (float.IsNaN(values[i])) Swap(values, first++, i);
        var bits = MemoryMarshal.Cast<float, int>(values[first..]);
        TransformSingle(bits);
        try { SortInt32(bits); }
        finally { TransformSingle(bits); }
    }

    private static void TransformSingle(Span<int> bits)
    {
        ref int data = ref MemoryMarshal.GetReference(bits);
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var mask = Vector512.Create(int.MaxValue);
            for (; i <= bits.Length - 16; i += 16)
            {
                var v = Vector512.LoadUnsafe(ref data, (nuint)i);
                (v ^ ((v >> 31) & mask)).StoreUnsafe(ref data, (nuint)i);
            }
        }
        for (; i < bits.Length; i++)
        {
            ref int value = ref Unsafe.Add(ref data, i);
            value ^= (value >> 31) & int.MaxValue;
        }
    }

    private static void SortHalf(Span<Half> values)
    {
        if (TryOrdered(values, Comparer<Half>.Default)) return;
        if (values.Length <= 32) { InsertionSort(values, Comparer<Half>.Default); return; }
        int first = 0;
        for (int i = 0; i < values.Length; i++)
            if (Half.IsNaN(values[i])) Swap(values, first++, i);
        var bits = MemoryMarshal.Cast<Half, ushort>(values[first..]);
        foreach (ref ushort value in bits)
            value ^= (ushort)((value & 0x8000) != 0 ? 0xffff : 0x8000);
        try { SortUInt16(bits); }
        finally
        {
            foreach (ref ushort value in bits)
                value ^= (ushort)((value & 0x8000) == 0 ? 0xffff : 0x8000);
        }
    }

    private static bool TryOrderedInteger<T>(Span<T> values) where T : unmanaged, IBinaryInteger<T>
    {
        if (values.Length < 2) return true;
        int i = 1;
        while (i < values.Length && values[i - 1] == values[i]) i++;
        if (i == values.Length) return true;
        bool descending = values[i - 1] > values[i];
        while (++i < values.Length)
            if (descending ? values[i - 1] < values[i] : values[i - 1] > values[i])
                return false;
        if (descending) values.Reverse();
        return true;
    }

    private static void InsertionInteger<T>(Span<T> values) where T : unmanaged, IBinaryInteger<T>
    {
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            int j = i;
            while (j > 0 && values[j - 1] > value)
            {
                values[j] = values[j - 1];
                j--;
            }
            values[j] = value;
        }
    }

    private static void XorKeys<T>(Span<T> values, T mask) where T : unmanaged, IBinaryInteger<T>
    {
        ref T data = ref MemoryMarshal.GetReference(values);
        int i = 0;
        if (Vector512.IsHardwareAccelerated && Vector512<T>.IsSupported)
        {
            var v = Vector512.Create(mask);
            int lanes = Vector512<T>.Count;
            for (; i <= values.Length - lanes; i += lanes)
                (Vector512.LoadUnsafe(ref data, (nuint)i) ^ v).StoreUnsafe(ref data, (nuint)i);
        }
        for (; i < values.Length; i++)
            Unsafe.Add(ref data, i) ^= mask;
    }

    private static void PrefixPrimitive(Span<int> histogram)
    {
        int sum = 0;
        for (int i = 0; i < histogram.Length; i++)
        {
            int count = histogram[i];
            histogram[i] = sum;
            sum += count;
        }
    }

    private static void SortInt128(Span<Int128> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        if (values.Length <= 1048576 && TryProjected128(values, Comparer<Int128>.Default, true)) return;
        var bits = MemoryMarshal.Cast<Int128, UInt128>(values);
        UInt128 sign = UInt128.One << 127;
        foreach (ref UInt128 value in bits) value ^= sign;
        try { SortRadix128(bits); }
        finally { foreach (ref UInt128 value in bits) value ^= sign; }
    }

    private static void SortUnsigned128(Span<UInt128> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        if (values.Length <= 1048576 && TryProjected128(values, Comparer<UInt128>.Default, false)) return;
        SortRadix128(values);
    }

    private static void SortRadix128(Span<UInt128> values)
    {
        if (values.Length <= 32) { InsertionInteger(values); return; }
        Span<int> counts = stackalloc int[16 * 256];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        foreach (UInt128 value in values)
        {
            ulong low = (ulong)value, high = (ulong)(value >> 64);
            Unsafe.Add(ref h, 0 + (byte)(low >> 0))++;
            Unsafe.Add(ref h, 256 + (byte)(low >> 8))++;
            Unsafe.Add(ref h, 512 + (byte)(low >> 16))++;
            Unsafe.Add(ref h, 768 + (byte)(low >> 24))++;
            Unsafe.Add(ref h, 1024 + (byte)(low >> 32))++;
            Unsafe.Add(ref h, 1280 + (byte)(low >> 40))++;
            Unsafe.Add(ref h, 1536 + (byte)(low >> 48))++;
            Unsafe.Add(ref h, 1792 + (byte)(low >> 56))++;
            Unsafe.Add(ref h, 2048 + (byte)(high >> 0))++;
            Unsafe.Add(ref h, 2304 + (byte)(high >> 8))++;
            Unsafe.Add(ref h, 2560 + (byte)(high >> 16))++;
            Unsafe.Add(ref h, 2816 + (byte)(high >> 24))++;
            Unsafe.Add(ref h, 3072 + (byte)(high >> 32))++;
            Unsafe.Add(ref h, 3328 + (byte)(high >> 40))++;
            Unsafe.Add(ref h, 3584 + (byte)(high >> 48))++;
            Unsafe.Add(ref h, 3840 + (byte)(high >> 56))++;
        }
        UInt128[]? rented = null;
        Span<UInt128> scratch = values.Length <= 4096 ? stackalloc UInt128[values.Length] :
            (rented = ArrayPool<UInt128>.Shared.Rent(values.Length)).AsSpan(0, values.Length);
        try
        {
            UInt128 first = values[0];
            bool inScratch = false;
            for (int digit = 0; digit < 16; digit++)
            {
                int shift = digit * 8;
                var histogram = counts.Slice(digit * 256, 256);
                if (histogram[(byte)(first >> shift)] == values.Length) continue;
                PrefixPrimitive(histogram);
                if (inScratch) Scatter128(scratch, values, histogram, shift);
                else Scatter128(values, scratch, histogram, shift);
                inScratch = !inScratch;
            }
            if (inScratch) scratch.CopyTo(values);
        }
        finally { if (rented is not null) ArrayPool<UInt128>.Shared.Return(rented); }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Scatter128(ReadOnlySpan<UInt128> source, Span<UInt128> target, Span<int> counts, int shift)
    {
        ref UInt128 input = ref MemoryMarshal.GetReference(source);
        ref UInt128 output = ref MemoryMarshal.GetReference(target);
        ref int h = ref MemoryMarshal.GetReference(counts);
        bool high = shift >= 64;
        shift &= 63;
        for (int i = 0; i < source.Length; i++)
        {
            UInt128 value = Unsafe.Add(ref input, i);
            ulong word = high ? (ulong)(value >> 64) : (ulong)value;
            Unsafe.Add(ref output, Unsafe.Add(ref h, (byte)(word >> shift))++) = value;
        }
    }

    private static readonly UInt128[] DecimalPowers =
    [
        ((UInt128)0UL << 64) | 1UL,
        ((UInt128)0UL << 64) | 10UL,
        ((UInt128)0UL << 64) | 100UL,
        ((UInt128)0UL << 64) | 1000UL,
        ((UInt128)0UL << 64) | 10000UL,
        ((UInt128)0UL << 64) | 100000UL,
        ((UInt128)0UL << 64) | 1000000UL,
        ((UInt128)0UL << 64) | 10000000UL,
        ((UInt128)0UL << 64) | 100000000UL,
        ((UInt128)0UL << 64) | 1000000000UL,
        ((UInt128)0UL << 64) | 10000000000UL,
        ((UInt128)0UL << 64) | 100000000000UL,
        ((UInt128)0UL << 64) | 1000000000000UL,
        ((UInt128)0UL << 64) | 10000000000000UL,
        ((UInt128)0UL << 64) | 100000000000000UL,
        ((UInt128)0UL << 64) | 1000000000000000UL,
        ((UInt128)0UL << 64) | 10000000000000000UL,
        ((UInt128)0UL << 64) | 100000000000000000UL,
        ((UInt128)0UL << 64) | 1000000000000000000UL,
        ((UInt128)0UL << 64) | 10000000000000000000UL,
        ((UInt128)5UL << 64) | 7766279631452241920UL,
        ((UInt128)54UL << 64) | 3875820019684212736UL,
        ((UInt128)542UL << 64) | 1864712049423024128UL,
        ((UInt128)5421UL << 64) | 200376420520689664UL,
        ((UInt128)54210UL << 64) | 2003764205206896640UL,
        ((UInt128)542101UL << 64) | 1590897978359414784UL,
        ((UInt128)5421010UL << 64) | 15908979783594147840UL,
        ((UInt128)54210108UL << 64) | 11515845246265065472UL,
        ((UInt128)542101086UL << 64) | 4477988020393345024UL,
        ((UInt128)5421010862UL << 64) | 7886392056514347008UL
    ];

    // Normalize a decimal coefficient to 29 digits and encode its decimal
    // exponent. Equal numeric values have the same prefix regardless of scale.
    private static ulong DecimalPrefix(decimal value, Span<int> parts)
    {
        decimal.GetBits(value, parts);
        ulong low = (uint)parts[0] | ((ulong)(uint)parts[1] << 32);
        uint high = (uint)parts[2];
        if ((low | high) == 0) return 0x8000000000000000UL;
        UInt128 magnitude = ((UInt128)high << 64) | low;
        int bitLength = high == 0 ? BitOperations.Log2(low) + 1 : BitOperations.Log2(high) + 65;
        int digits = (bitLength * 1233) >> 12;
        if (magnitude >= DecimalPowers[digits]) digits++;
        UInt128 normalized = magnitude * DecimalPowers[29 - digits];
        int exponent = digits - ((parts[3] >> 16) & 255) + 28;
        ulong key = 0x8000000000000000UL | ((ulong)exponent << 57) | (ulong)(normalized >> 40);
        return parts[3] < 0 ? ~key : key;
    }

    private static bool TryProjected128<T, TComparer>(Span<T> values, TComparer comparer, bool signed)
        where T : unmanaged where TComparer : IComparer<T>
    {
        int length = values.Length;
        int indexBits = BitOperations.Log2((uint)(length - 1)) + 1;
        ulong indexMask = (1UL << indexBits) - 1;
        ulong[]? rentedKeys = null;
        Span<ulong> keys = length <= 8192 ? stackalloc ulong[length] :
            (rentedKeys = ArrayPool<ulong>.Shared.Rent(length)).AsSpan(0, length);
        Span<int> parts = stackalloc int[4];
        try
        {
            ulong first = 0, varying = 0;
            for (int i = 0; i < length; i++)
            {
                ulong prefix;
                if (typeof(T) == typeof(decimal))
                    prefix = DecimalPrefix(Unsafe.As<T, decimal>(ref values[i]), parts);
                else
                    prefix = (ulong)(Unsafe.As<T, UInt128>(ref values[i]) >> 64) ^
                        (signed ? 0x8000000000000000UL : 0UL);
                prefix &= ~indexMask;
                if (i == 0) first = prefix;
                varying |= prefix ^ first;
                keys[i] = prefix | (uint)i;
            }
            if (varying == 0) return false;
            SortUInt64(keys, indexBits / 8);

            T[]? rented = null;
            Span<T> original = length <= 4096 ? stackalloc T[length] :
                (rented = ArrayPool<T>.Shared.Rent(length)).AsSpan(0, length);
            try
            {
                values.CopyTo(original);
                for (int i = 0; i < length; i++)
                    values[i] = original[(int)(keys[i] & indexMask)];
                for (int start = 0; start < length;)
                {
                    int end = start + 1;
                    ulong prefix = keys[start] & ~indexMask;
                    while (end < length && (keys[end] & ~indexMask) == prefix) end++;
                    int count = end - start;
                    if (count > 1)
                        IntroSort(values.Slice(start, count), comparer, 2 * (BitOperations.Log2((uint)count) + 1));
                    start = end;
                }
            }
            finally { if (rented is not null) ArrayPool<T>.Shared.Return(rented); }
            return true;
        }
        finally { if (rentedKeys is not null) ArrayPool<ulong>.Shared.Return(rentedKeys); }
    }

    private readonly struct DecimalComparer : IComparer<decimal>
    {
        public int Compare(decimal x, decimal y) => decimal.Compare(x, y);
    }

    private static void SortDecimal(Span<decimal> values)
    {
        if (TryOrdered(values, Comparer<decimal>.Default)) return;
        if (values.Length >= 64 && values.Length <= 1048576 && TryProjected128(values, new DecimalComparer(), false)) return;
        // Decimal is not a CLR primitive. Keep exact comparisons and avoid
        // virtual comparer dispatch; the original encoding of equal values is preserved.
        IntroSort(values, new DecimalComparer(), 2 * (BitOperations.Log2((uint)values.Length) + 1));
    }
}
