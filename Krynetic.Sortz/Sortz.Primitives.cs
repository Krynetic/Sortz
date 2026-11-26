using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Krynetic.Sortz;

public static partial class Sortz
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
        else if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= bytes.Length - 32; i += 32)
                count += BitOperations.PopCount(Vector256.ExtractMostSignificantBits(
                    Vector256.Equals(Vector256.LoadUnsafe(ref data, (nuint)i), Vector256<byte>.Zero)));
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= bytes.Length - 16; i += 16)
                count += BitOperations.PopCount(Vector128.ExtractMostSignificantBits(
                    Vector128.Equals(Vector128.LoadUnsafe(ref data, (nuint)i), Vector128<byte>.Zero)));
        }
        for (; i < bytes.Length; i++)
            count += Unsafe.Add(ref data, i) == 0 ? 1 : 0;
        values[..count].Clear();
        values[count..].Fill(true);
    }

    private static void SortByte(Span<byte> values)
    {
        if (!TryOrderedNumeric(values)) SortByteCore(values, 0);
    }
    private static void SortSByte(Span<sbyte> values)
    {
        if (!TryOrderedNumeric(values)) SortByteCore(MemoryMarshal.Cast<sbyte, byte>(values), 128);
    }

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

        if (Avx512BW.IsSupported && values.Length <= 256)
        {
            Span<ushort> keys = stackalloc ushort[values.Length];
            int i = 0;
            ref byte source = ref MemoryMarshal.GetReference(values);
            ref ushort target = ref MemoryMarshal.GetReference(keys);
            var mask = Vector512.Create((byte)sign);
            for (; i <= values.Length - 64; i += 64)
            {
                var (lower, upper) = Vector512.Widen(Vector512.LoadUnsafe(ref source, (nuint)i) ^ mask);
                lower.StoreUnsafe(ref target, (nuint)i);
                upper.StoreUnsafe(ref target, (nuint)(i + 32));
            }
            for (; i < values.Length; i++) keys[i] = (ushort)(values[i] ^ sign);
            PrimitiveSimdSort.SortUInt16(keys);
            i = 0;
            for (; i <= values.Length - 64; i += 64)
            {
                var lower = Vector512.LoadUnsafe(ref target, (nuint)i);
                var upper = Vector512.LoadUnsafe(ref target, (nuint)(i + 32));
                (Vector512.Narrow(lower, upper) ^ mask).StoreUnsafe(ref source, (nuint)i);
            }
            for (; i < values.Length; i++) values[i] = (byte)(keys[i] ^ sign);
            return;
        }

        bool striped = values.Length >= 2048;
        Span<int> counts = stackalloc int[striped ? 1024 : 256];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        ref byte data = ref MemoryMarshal.GetReference(values);
        int index = 0;
        if (striped)
        {
            for (; index <= values.Length - 4; index += 4)
            {
                Unsafe.Add(ref h, Unsafe.Add(ref data, index) ^ sign)++;
                Unsafe.Add(ref h, 256 + (Unsafe.Add(ref data, index + 1) ^ sign))++;
                Unsafe.Add(ref h, 512 + (Unsafe.Add(ref data, index + 2) ^ sign))++;
                Unsafe.Add(ref h, 768 + (Unsafe.Add(ref data, index + 3) ^ sign))++;
            }
        }
        for (; index < values.Length; index++)
            Unsafe.Add(ref h, Unsafe.Add(ref data, index) ^ sign)++;

        int offset = 0;
        for (int i = 0; i < 256; i++)
        {
            int frequency = Unsafe.Add(ref h, i);
            if (striped)
                frequency += Unsafe.Add(ref h, i + 256) + Unsafe.Add(ref h, i + 512) + Unsafe.Add(ref h, i + 768);
            if (frequency == 0) continue;
            byte value = (byte)(i ^ sign);
            if (frequency <= 8 && offset <= values.Length - 8)
                // The following buckets overwrite any excess bytes; never write outside the span.
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref data, offset), 0x0101010101010101UL * value);
            else
                values.Slice(offset, frequency).Fill(value);
            offset += frequency;
        }
    }

    private static void SortChar(Span<char> values) => SortUInt16(MemoryMarshal.Cast<char, ushort>(values));

    private static void SortInt16(Span<short> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length >= 256 && TrySparseRepair(values)) return;
        var bits = MemoryMarshal.Cast<short, ushort>(values);
        XorKeys(bits, (ushort)0x8000);
        try { SortUInt16(bits); }
        finally { XorKeys(bits, (ushort)0x8000); }
    }

    private static void SortUInt16(Span<ushort> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length >= 256 && TrySparseRepair(values)) return;
        int length = values.Length;
        if (length <= 32) { InsertionInteger(values); return; }
        if (Avx512BW.IsSupported && length <= 1024)
        {
            PrimitiveSimdSort.SortUInt16(values);
            return;
        }
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
        if (TryOrderedNumeric(values)) return;
        if (values.Length >= 256 && TrySparseRepair(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        // High-digit distribution avoids the large register network when
        // the input is sufficiently dispersed. Skewed inputs fall back.
        if (values.Length is >= 512 and <= 2048)
        {
            var keys = MemoryMarshal.Cast<long, ulong>(values);
            XorKeys(keys, 0x8000000000000000UL);
            try
            {
                if (!TryBucketUInt64(keys))
                    SortPrimitiveUInt64(keys);
            }
            finally { XorKeys(keys, 0x8000000000000000UL); }
            return;
        }
        var bits = MemoryMarshal.Cast<long, ulong>(values);
        XorKeys(bits, 0x8000000000000000UL);
        try { SortPrimitiveUInt64(bits); }
        finally { XorKeys(bits, 0x8000000000000000UL); }
    }

    private static void SortUnsigned64(Span<ulong> values)
    {
        if (TryOrderedInteger(values)) return;
        if (values.Length >= 256 && TrySparseRepair(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        SortPrimitiveUInt64(values);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortPrimitiveUInt64(Span<ulong> values)
    {
        if (values.Length is >= 4096 and <= 65536 &&
            (TryProjectedCompact(values, Comparer<ulong>.Default, false) || TryBucketUInt64(values)))
            return;
        if (values.Length is > 65536 and <= 262144 && TryBucketUInt64(values))
            return;
        SortUInt64(values);
    }

    // A single high-digit scatter, then exact local sorting into the original
    // destination. Reject dense/skewed distributions before touching the input.
    private static bool TryBucketUInt64(Span<ulong> values)
    {
        int length = values.Length;
        int width = Math.Clamp(BitOperations.Log2((uint)length) - 1, 9, length > 65536 ? 14 : 13);
        int buckets = 1 << width, shift = 64 - width;
        Span<int> counts = stackalloc int[buckets];
        counts.Clear();
        ref int h = ref MemoryMarshal.GetReference(counts);
        ref ulong input = ref MemoryMarshal.GetReference(values);
        for (int i = 0; i < length; i++)
            if (++Unsafe.Add(ref h, (int)(Unsafe.Add(ref input, i) >> shift)) > 48)
                return false;

        PrefixPrimitive(counts);
        ulong[]? rented = null;
        Span<ulong> scratch = length <= 8192 ? stackalloc ulong[length] :
            (rented = ArrayPool<ulong>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            ref ulong output = ref MemoryMarshal.GetReference(scratch);
            for (int i = 0; i < length; i++)
            {
                ulong x = Unsafe.Add(ref input, i);
                Unsafe.Add(ref output, Unsafe.Add(ref h, (int)(x >> shift))++) = x;
            }

            int start = 0;
            for (int b = 0; b < buckets; b++)
            {
                int end = Unsafe.Add(ref h, b), size = end - start;
                if (size == 0) continue;
                ref ulong src = ref Unsafe.Add(ref output, start);
                ref ulong dst = ref Unsafe.Add(ref input, start);
                ulong a = src;
                if (size == 1) dst = a;
                else if (size == 2)
                {
                    ulong z = Unsafe.Add(ref src, 1);
                    dst = Math.Min(a, z);
                    Unsafe.Add(ref dst, 1) = Math.Max(a, z);
                }
                else if (size == 3)
                {
                    ulong z = Unsafe.Add(ref src, 1), d = Unsafe.Add(ref src, 2);
                    ExchangeUInt64(ref a, ref z);
                    ExchangeUInt64(ref z, ref d);
                    ExchangeUInt64(ref a, ref z);
                    dst = a;
                    Unsafe.Add(ref dst, 1) = z;
                    Unsafe.Add(ref dst, 2) = d;
                }
                else if (size == 4)
                {
                    ulong z = Unsafe.Add(ref src, 1), d = Unsafe.Add(ref src, 2), e = Unsafe.Add(ref src, 3);
                    ExchangeUInt64(ref a, ref z);
                    ExchangeUInt64(ref d, ref e);
                    ExchangeUInt64(ref a, ref d);
                    ExchangeUInt64(ref z, ref e);
                    ExchangeUInt64(ref z, ref d);
                    dst = a;
                    Unsafe.Add(ref dst, 1) = z;
                    Unsafe.Add(ref dst, 2) = d;
                    Unsafe.Add(ref dst, 3) = e;
                }
                else
                {
                    dst = a;
                    for (int i = 1; i < size; i++)
                    {
                        ulong x = Unsafe.Add(ref src, i);
                        int j = i;
                        while (j > 0 && Unsafe.Add(ref dst, j - 1) > x)
                        {
                            Unsafe.Add(ref dst, j) = Unsafe.Add(ref dst, j - 1);
                            j--;
                        }
                        Unsafe.Add(ref dst, j) = x;
                    }
                }
                start = end;
            }
            return true;
        }
        finally { if (rented is not null) ArrayPool<ulong>.Shared.Return(rented); }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExchangeUInt64(ref ulong a, ref ulong b)
    {
        ulong x = a, y = b;
        a = Math.Min(x, y);
        b = Math.Max(x, y);
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
        // CompareTo orders NaNs before numbers; SIMD comparisons do not.
        // Skip a leading NaN run before checking the numeric suffix.
        if (float.IsNaN(values[0]))
        {
            int first = 1;
            while (first < values.Length && float.IsNaN(values[first])) first++;
            if (TryOrderedNumeric(values[first..])) return;
        }
        else if (TryOrderedNumeric(values)) return;
        if (values.Length >= 256 && TrySparseRepair(values)) return;
        if (values.Length <= 32) { InsertionSort(values, Comparer<float>.Default); return; }
        var bits = MemoryMarshal.Cast<float, uint>(values);
        TransformSingleKeys(bits, false);
        try { SortInt32(MemoryMarshal.Cast<uint, int>(bits)); }
        finally { TransformSingleKeys(bits, true); }
    }

    // Rotate positive NaN encodings ahead of numbers, alongside negative NaNs.
    // This is a bijection over all 2^32 bit patterns, preserving NaN payloads
    // and both signed zeros while agreeing with Single.CompareTo.
    private static void TransformSingleKeys(Span<uint> bits, bool inverse)
    {
        const uint Sign = 0x80000000u, Nan = 0x007fffffu, Rotate = 0xff000002u;
        ref uint data = ref MemoryMarshal.GetReference(bits);
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            var sign = Vector512.Create(Sign);
            var nan = Vector512.Create(Nan);
            var rotate = Vector512.Create(Rotate);
            var limit = Vector512.Create(inverse ? 2 * Nan : 0xff800000u);
            for (; i <= bits.Length - 16; i += 16)
            {
                var v = Vector512.LoadUnsafe(ref data, (nuint)i);
                if (!inverse)
                {
                    v ^= ((v.AsInt32() >> 31).AsUInt32() | sign);
                    v = Vector512.ConditionalSelect(Vector512.LessThan(v, nan), v,
                        Vector512.ConditionalSelect(Vector512.GreaterThan(v, limit), v - rotate, v + nan));
                    v ^= sign;
                }
                else
                {
                    v ^= sign;
                    v = Vector512.ConditionalSelect(Vector512.LessThan(v, nan), v,
                        Vector512.ConditionalSelect(Vector512.LessThan(v, limit), v + rotate, v - nan));
                    v ^= (~(v.AsInt32() >> 31).AsUInt32() | sign);
                }
                v.StoreUnsafe(ref data, (nuint)i);
            }
        }

        for (; i < bits.Length; i++)
        {
            ref uint item = ref Unsafe.Add(ref data, i);
            uint v = item;
            if (!inverse)
            {
                v ^= (uint)((int)v >> 31) | Sign;
                v = v < Nan ? v : v > 0xff800000u ? v - Rotate : v + Nan;
                item = v ^ Sign;
            }
            else
            {
                v ^= Sign;
                v = v < Nan ? v : v < 2 * Nan ? v + Rotate : v - Nan;
                item = v ^ (~(uint)((int)v >> 31) | Sign);
            }
        }
    }

    private static void SortHalf(Span<Half> values)
    {
        if (TryOrdered(values, Comparer<Half>.Default)) return;
        if (values.Length <= 32) { InsertionSort(values, Comparer<Half>.Default); return; }
        var bits = MemoryMarshal.Cast<Half, ushort>(values);
        TransformHalfKeys(bits, false);
        try { SortUInt16(bits); }
        finally { TransformHalfKeys(bits, true); }
    }

    // The same reversible rotation in the 16-bit IEEE-754 domain.
    private static void TransformHalfKeys(Span<ushort> bits, bool inverse)
    {
        const ushort Sign = 0x8000, Nan = 0x03ff, Rotate = 0xf802;
        ref ushort data = ref MemoryMarshal.GetReference(bits);
        int i = 0;
        if (Avx512BW.IsSupported)
        {
            var sign = Vector512.Create(Sign);
            var nan = Vector512.Create(Nan);
            var rotate = Vector512.Create(Rotate);
            var limit = Vector512.Create((ushort)(inverse ? 2 * Nan : 0xfc00));
            for (; i <= bits.Length - 32; i += 32)
            {
                var v = Vector512.LoadUnsafe(ref data, (nuint)i);
                if (!inverse)
                {
                    v ^= ((v.AsInt16() >> 15).AsUInt16() | sign);
                    v = Vector512.ConditionalSelect(Vector512.LessThan(v, nan), v,
                        Vector512.ConditionalSelect(Vector512.GreaterThan(v, limit), v - rotate, v + nan));
                }
                else
                {
                    v = Vector512.ConditionalSelect(Vector512.LessThan(v, nan), v,
                        Vector512.ConditionalSelect(Vector512.LessThan(v, limit), v + rotate, v - nan));
                    v ^= (~(v.AsInt16() >> 15).AsUInt16() | sign);
                }
                v.StoreUnsafe(ref data, (nuint)i);
            }
        }
        for (; i < bits.Length; i++)
        {
            ref ushort item = ref Unsafe.Add(ref data, i);
            uint v = item;
            if (!inverse)
            {
                v ^= (v & Sign) != 0 ? 0xffffu : Sign;
                v = v < Nan ? v : v > 0xfc00 ? v - Rotate : v + Nan;
            }
            else
            {
                v = v < Nan ? v : v < 2 * Nan ? v + Rotate : v - Nan;
                v ^= (v & Sign) == 0 ? 0xffffu : Sign;
            }
            item = (ushort)v;
        }
    }

    private static bool TryOrderedInteger<T>(Span<T> values) where T : unmanaged, IBinaryInteger<T>
    {
        if (values.Length < 2) return true;
        return TryOrderedNumeric(values);
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
        if (values.Length >= 256 && TrySparseRepair(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        if (values.Length <= 65536 && TryProjectedCompact(values, Comparer<Int128>.Default, true)) return;
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
        if (values.Length >= 256 && TrySparseRepair(values)) return;
        if (values.Length <= 32) { InsertionInteger(values); return; }
        if (values.Length <= 65536 && TryProjectedCompact(values, Comparer<UInt128>.Default, false)) return;
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

    // Use the high bits of a wide integer (or decimal prefix) as a monotone coarse key,
    // packing the original index into the remaining bits. Full comparisons
    // resolve collisions; dense prefixes fall back to the wider projection.
    private static bool TryProjectedCompact<T, TComparer>(Span<T> values, TComparer comparer, bool signed)
        where T : unmanaged where TComparer : IComparer<T>
    {
        int length = values.Length;
        int indexBits = BitOperations.Log2((uint)(length - 1)) + 1;
        uint indexMask = (1u << indexBits) - 1;
        uint sign = signed ? 0u : 0x80000000u;
        int[]? rentedKeys = null;
        Span<int> keys = length <= 16384 ? stackalloc int[length] :
            (rentedKeys = ArrayPool<int>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            ref T input = ref MemoryMarshal.GetReference(values);
            ref int keyData = ref MemoryMarshal.GetReference(keys);
            uint first = 0, varying = 0;
            Span<int> parts = stackalloc int[4];
            int index = 0;
            if (typeof(T) == typeof(ulong) && Vector512.IsHardwareAccelerated)
            {
                ref ulong words = ref Unsafe.As<T, ulong>(ref input);
                var signMask = Vector512.Create(sign);
                var prefixMask = Vector512.Create(~indexMask);
                var offsets = Vector512.Create(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u,
                    8u, 9u, 10u, 11u, 12u, 13u, 14u, 15u);
                first = ((uint)(words >> 32) ^ sign) & ~indexMask;
                var firstVector = Vector512.Create(first);
                var differences = Vector512<uint>.Zero;
                for (; index <= length - 16; index += 16)
                {
                    var a = Vector512.LoadUnsafe(ref words, (nuint)index);
                    var b = Vector512.LoadUnsafe(ref words, (nuint)(index + 8));
                    var prefixes = (Vector512.Narrow(a >> 32, b >> 32) ^ signMask) & prefixMask;
                    differences |= prefixes ^ firstVector;
                    (prefixes | (offsets + Vector512.Create((uint)index))).AsInt32()
                        .StoreUnsafe(ref keyData, (nuint)index);
                }
                for (int lane = 0; lane < 16; lane++)
                    varying |= differences.GetElement(lane);
            }
            else if (typeof(T) != typeof(decimal) && typeof(T) != typeof(ulong) && BitConverter.IsLittleEndian && Avx512F.IsSupported)
            {
                ref uint words = ref Unsafe.As<T, uint>(ref input);
                var pick = Vector512.Create(3u, 7u, 11u, 15u, 19u, 23u, 27u, 31u,
                    3u, 7u, 11u, 15u, 19u, 23u, 27u, 31u);
                var signMask = Vector256.Create(sign);
                var prefixMask = Vector256.Create(~indexMask);
                var offsets = Vector256.Create(0u, 1u, 2u, 3u, 4u, 5u, 6u, 7u);
                first = (Unsafe.Add(ref words, 3) ^ sign) & ~indexMask;
                var firstVector = Vector256.Create(first);
                var differences = Vector256<uint>.Zero;
                for (; index <= length - 8; index += 8)
                {
                    var a = Vector512.LoadUnsafe(ref words, (nuint)index * 4);
                    var b = Vector512.LoadUnsafe(ref words, (nuint)index * 4 + 16);
                    var prefixes = (Avx512F.PermuteVar16x32x2(a, pick, b).GetLower() ^ signMask) & prefixMask;
                    differences |= prefixes ^ firstVector;
                    (prefixes | (offsets + Vector256.Create((uint)index))).AsInt32()
                        .StoreUnsafe(ref keyData, (nuint)index);
                }
                for (int lane = 0; lane < 8; lane++)
                    varying |= differences.GetElement(lane);
            }
            for (; index < length; index++)
            {
                uint high = typeof(T) == typeof(decimal)
                    ? (uint)(DecimalPrefix(Unsafe.As<T, decimal>(ref Unsafe.Add(ref input, index)), parts) >> 32)
                    : typeof(T) == typeof(ulong)
                        ? (uint)(Unsafe.As<T, ulong>(ref Unsafe.Add(ref input, index)) >> 32)
                        : (uint)(Unsafe.As<T, UInt128>(ref Unsafe.Add(ref input, index)) >> 96);
                uint prefix = (high ^ sign) & ~indexMask;
                if (index == 0) first = prefix;
                varying |= prefix ^ first;
                Unsafe.Add(ref keyData, index) = (int)(prefix | (uint)index);
            }

            if (BitOperations.PopCount(varying) < 4) return false;
            SortProjectedInt32(keys, indexBits);
            int maxCollision = Math.Max(64, length / 8);
            for (int i = maxCollision; i < length; i += maxCollision)
                if (((uint)keys[i] & ~indexMask) == ((uint)keys[i - maxCollision] & ~indexMask))
                    return false;

            T[]? rented = null;
            Span<T> original = length <= 4096 ? stackalloc T[length] :
                (rented = ArrayPool<T>.Shared.Rent(length)).AsSpan(0, length);
            try
            {
                values.CopyTo(original);
                ref T copy = ref MemoryMarshal.GetReference(original);
                for (int i = 0; i < length; i++)
                    Unsafe.Add(ref input, i) = Unsafe.Add(ref copy, (int)((uint)Unsafe.Add(ref keyData, i) & indexMask));

                // Most projected keys are distinct. Scan adjacent keys once,
                // entering collision repair only when their prefixes match.
                for (int i = 1; i < length; i++)
                {
                    i = NextProjectedCollision(ref keyData, i, length, indexMask);
                    if (i == length) break;
                    int start = i - 1;
                    uint prefix = (uint)Unsafe.Add(ref keyData, start) & ~indexMask;
                    int end = i + 1;
                    while (end < length && ((uint)Unsafe.Add(ref keyData, end) & ~indexMask) == prefix) end++;
                    int count = end - start;
                    if (count == 2)
                    {
                        ref T left = ref Unsafe.Add(ref input, start);
                        ref T right = ref Unsafe.Add(ref input, i);
                        if (comparer.Compare(left, right) > 0) (left, right) = (right, left);
                    }
                    else
                        IntroSort(values.Slice(start, count), comparer, 2 * (BitOperations.Log2((uint)count) + 1));
                    i = end;
                }
            }
            finally { if (rented is not null) ArrayPool<T>.Shared.Return(rented); }
            return true;
        }
        finally { if (rentedKeys is not null) ArrayPool<int>.Shared.Return(rentedKeys); }
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
        if (values.Length is >= 64 and <= 65536 && TryProjectedCompact(values, new DecimalComparer(), false)) return;
        if (values.Length >= 64 && values.Length <= 1048576 && TryProjected128(values, new DecimalComparer(), false)) return;
        // Decimal is not a CLR primitive. Keep exact comparisons and avoid
        // virtual comparer dispatch; the original encoding of equal values is preserved.
        IntroSort(values, new DecimalComparer(), 2 * (BitOperations.Log2((uint)values.Length) + 1));
    }
}
