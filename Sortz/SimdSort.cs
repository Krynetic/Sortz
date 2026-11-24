using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Sortz;

internal static class SimdSort
{
    private static readonly Vector512<int> ShuffleInt321 = Vector512.Create(1, 0, 3, 2, 5, 4, 7, 6, 9, 8, 11, 10, 13, 12, 15, 14);
    private static readonly Vector512<int> ShuffleInt322 = Vector512.Create(2, 3, 0, 1, 6, 7, 4, 5, 10, 11, 8, 9, 14, 15, 12, 13);
    private static readonly Vector512<int> ShuffleInt324 = Vector512.Create(4, 5, 6, 7, 0, 1, 2, 3, 12, 13, 14, 15, 8, 9, 10, 11);
    private static readonly Vector512<int> ShuffleInt328 = Vector512.Create(8, 9, 10, 11, 12, 13, 14, 15, 0, 1, 2, 3, 4, 5, 6, 7);
    private static readonly Vector512<int> ReverseInt32 = Vector512.Create(15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0);
    private static readonly Vector512<int> MaskInt322_1 = Vector512.Create(-1, 0, 0, -1, -1, 0, 0, -1, -1, 0, 0, -1, -1, 0, 0, -1);
    private static readonly Vector512<int> MaskInt324_2 = Vector512.Create(-1, -1, 0, 0, 0, 0, -1, -1, -1, -1, 0, 0, 0, 0, -1, -1);
    private static readonly Vector512<int> MaskInt324_1 = Vector512.Create(-1, 0, -1, 0, 0, -1, 0, -1, -1, 0, -1, 0, 0, -1, 0, -1);
    private static readonly Vector512<int> MaskInt328_4 = Vector512.Create(-1, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, -1, -1, -1, -1);
    private static readonly Vector512<int> MaskInt328_2 = Vector512.Create(-1, -1, 0, 0, -1, -1, 0, 0, 0, 0, -1, -1, 0, 0, -1, -1);
    private static readonly Vector512<int> MaskInt328_1 = Vector512.Create(-1, 0, -1, 0, -1, 0, -1, 0, 0, -1, 0, -1, 0, -1, 0, -1);
    private static readonly Vector512<int> MaskInt3216_8 = Vector512.Create(-1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Vector512<int> MaskInt3216_4 = Vector512.Create(-1, -1, -1, -1, 0, 0, 0, 0, -1, -1, -1, -1, 0, 0, 0, 0);
    private static readonly Vector512<int> MaskInt3216_2 = Vector512.Create(-1, -1, 0, 0, -1, -1, 0, 0, -1, -1, 0, 0, -1, -1, 0, 0);
    private static readonly Vector512<int> MaskInt3216_1 = Vector512.Create(-1, 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 0, -1, 0);

    public static void SortInt32(Span<int> values)
    {
        if (values.Length == 128)
        {
            Sort128Int32(values);
            return;
        }

        int size = Math.Max(16, (int)BitOperations.RoundUpToPowerOf2((uint)values.Length));
        if (size == values.Length)
        {
            SortInt32Core(values);
            return;
        }
        Span<int> padded = stackalloc int[size];
        values.CopyTo(padded);
        padded[values.Length..].Fill(int.MaxValue);
        SortInt32Core(padded);
        padded[..values.Length].CopyTo(values);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Sort128Int32(Span<int> values)
    {
        ref int data = ref MemoryMarshal.GetReference(values);
        var v0 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 0));
        var v1 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 16));
        var v2 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 32));
        var v3 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 48));
        var v4 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 64));
        var v5 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 80));
        var v6 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 96));
        var v7 = SortVectorInt32(Vector512.LoadUnsafe(ref data, 112));
        var r2_0 = Vector512.Shuffle(v1, ReverseInt32);
        var h2_0 = Vector512.Max(v0, r2_0);
        v0 = Vector512.Min(v0, r2_0);
        v1 = h2_0;
        v0 = MergeVectorInt32(v0);
        v1 = MergeVectorInt32(v1);
        var r2_2 = Vector512.Shuffle(v3, ReverseInt32);
        var h2_2 = Vector512.Max(v2, r2_2);
        v2 = Vector512.Min(v2, r2_2);
        v3 = h2_2;
        v2 = MergeVectorInt32(v2);
        v3 = MergeVectorInt32(v3);
        var r2_4 = Vector512.Shuffle(v5, ReverseInt32);
        var h2_4 = Vector512.Max(v4, r2_4);
        v4 = Vector512.Min(v4, r2_4);
        v5 = h2_4;
        v4 = MergeVectorInt32(v4);
        v5 = MergeVectorInt32(v5);
        var r2_6 = Vector512.Shuffle(v7, ReverseInt32);
        var h2_6 = Vector512.Max(v6, r2_6);
        v6 = Vector512.Min(v6, r2_6);
        v7 = h2_6;
        v6 = MergeVectorInt32(v6);
        v7 = MergeVectorInt32(v7);
        var r4_0 = Vector512.Shuffle(v3, ReverseInt32);
        var h4_0 = Vector512.Max(v0, r4_0);
        v0 = Vector512.Min(v0, r4_0);
        var r4_1 = Vector512.Shuffle(v2, ReverseInt32);
        var h4_1 = Vector512.Max(v1, r4_1);
        v1 = Vector512.Min(v1, r4_1);
        v2 = h4_0;
        v3 = h4_1;
        var t4_1_0 = Vector512.Max(v0, v1);
        v0 = Vector512.Min(v0, v1);
        v1 = t4_1_0;
        var t4_1_2 = Vector512.Max(v2, v3);
        v2 = Vector512.Min(v2, v3);
        v3 = t4_1_2;
        v0 = MergeVectorInt32(v0);
        v1 = MergeVectorInt32(v1);
        v2 = MergeVectorInt32(v2);
        v3 = MergeVectorInt32(v3);
        var r4_4 = Vector512.Shuffle(v7, ReverseInt32);
        var h4_4 = Vector512.Max(v4, r4_4);
        v4 = Vector512.Min(v4, r4_4);
        var r4_5 = Vector512.Shuffle(v6, ReverseInt32);
        var h4_5 = Vector512.Max(v5, r4_5);
        v5 = Vector512.Min(v5, r4_5);
        v6 = h4_4;
        v7 = h4_5;
        var t4_1_4 = Vector512.Max(v4, v5);
        v4 = Vector512.Min(v4, v5);
        v5 = t4_1_4;
        var t4_1_6 = Vector512.Max(v6, v7);
        v6 = Vector512.Min(v6, v7);
        v7 = t4_1_6;
        v4 = MergeVectorInt32(v4);
        v5 = MergeVectorInt32(v5);
        v6 = MergeVectorInt32(v6);
        v7 = MergeVectorInt32(v7);
        var r8_0 = Vector512.Shuffle(v7, ReverseInt32);
        var h8_0 = Vector512.Max(v0, r8_0);
        v0 = Vector512.Min(v0, r8_0);
        var r8_1 = Vector512.Shuffle(v6, ReverseInt32);
        var h8_1 = Vector512.Max(v1, r8_1);
        v1 = Vector512.Min(v1, r8_1);
        var r8_2 = Vector512.Shuffle(v5, ReverseInt32);
        var h8_2 = Vector512.Max(v2, r8_2);
        v2 = Vector512.Min(v2, r8_2);
        var r8_3 = Vector512.Shuffle(v4, ReverseInt32);
        var h8_3 = Vector512.Max(v3, r8_3);
        v3 = Vector512.Min(v3, r8_3);
        v4 = h8_0;
        v5 = h8_1;
        v6 = h8_2;
        v7 = h8_3;
        var t8_2_0 = Vector512.Max(v0, v2);
        v0 = Vector512.Min(v0, v2);
        v2 = t8_2_0;
        var t8_2_1 = Vector512.Max(v1, v3);
        v1 = Vector512.Min(v1, v3);
        v3 = t8_2_1;
        var t8_2_4 = Vector512.Max(v4, v6);
        v4 = Vector512.Min(v4, v6);
        v6 = t8_2_4;
        var t8_2_5 = Vector512.Max(v5, v7);
        v5 = Vector512.Min(v5, v7);
        v7 = t8_2_5;
        var t8_1_0 = Vector512.Max(v0, v1);
        v0 = Vector512.Min(v0, v1);
        v1 = t8_1_0;
        var t8_1_2 = Vector512.Max(v2, v3);
        v2 = Vector512.Min(v2, v3);
        v3 = t8_1_2;
        var t8_1_4 = Vector512.Max(v4, v5);
        v4 = Vector512.Min(v4, v5);
        v5 = t8_1_4;
        var t8_1_6 = Vector512.Max(v6, v7);
        v6 = Vector512.Min(v6, v7);
        v7 = t8_1_6;
        v0 = MergeVectorInt32(v0);
        v1 = MergeVectorInt32(v1);
        v2 = MergeVectorInt32(v2);
        v3 = MergeVectorInt32(v3);
        v4 = MergeVectorInt32(v4);
        v5 = MergeVectorInt32(v5);
        v6 = MergeVectorInt32(v6);
        v7 = MergeVectorInt32(v7);
        v0.StoreUnsafe(ref data, 0);
        v1.StoreUnsafe(ref data, 16);
        v2.StoreUnsafe(ref data, 32);
        v3.StoreUnsafe(ref data, 48);
        v4.StoreUnsafe(ref data, 64);
        v5.StoreUnsafe(ref data, 80);
        v6.StoreUnsafe(ref data, 96);
        v7.StoreUnsafe(ref data, 112);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SortInt32Core(Span<int> values)
    {
        ref int data = ref MemoryMarshal.GetReference(values);
        const int lanes = 16;
        for (int i = 0; i < values.Length; i += lanes)
            SortVectorInt32(Vector512.LoadUnsafe(ref data, (nuint)i)).StoreUnsafe(ref data, (nuint)i);
        for (int width = lanes * 2; width <= values.Length; width *= 2)
        {
            int half = width / 2;
            for (int start = 0; start < values.Length; start += width)
            {
                for (int i = 0; i < half; i += lanes)
                {
                    nuint a = (nuint)(start + i), b = (nuint)(start + width - lanes - i);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.Shuffle(Vector512.LoadUnsafe(ref data, b), ReverseInt32);
                    Vector512.Min(left, right).StoreUnsafe(ref data, a);
                    Vector512.Max(left, right).StoreUnsafe(ref data, b);
                }
                // The upper half is now in reversed block order. Reverse it back
                // to make both halves bitonic in the same orientation.
                for (int i = 0; i < half / 2; i += lanes)
                {
                    nuint a = (nuint)(start + half + i), b = (nuint)(start + width - lanes - i);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.LoadUnsafe(ref data, b);
                    right.StoreUnsafe(ref data, a);
                    left.StoreUnsafe(ref data, b);
                }
                for (int distance = half / 2; distance >= lanes; distance /= 2)
                    for (int i = 0; i < width; i += lanes)
                    {
                        if ((i & distance) != 0) continue;
                        nuint a = (nuint)(start + i), b = (nuint)(start + i + distance);
                        var left = Vector512.LoadUnsafe(ref data, a);
                        var right = Vector512.LoadUnsafe(ref data, b);
                        Vector512.Min(left, right).StoreUnsafe(ref data, a);
                        Vector512.Max(left, right).StoreUnsafe(ref data, b);
                    }
                for (int i = 0; i < width; i += lanes)
                {
                    nuint offset = (nuint)(start + i);
                    MergeVectorInt32(Vector512.LoadUnsafe(ref data, offset)).StoreUnsafe(ref data, offset);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> SortVectorInt32(Vector512<int> v)
    {
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0xb1) : Vector512.Shuffle(v, ShuffleInt321), MaskInt322_1);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0x4e) : Vector512.Shuffle(v, ShuffleInt322), MaskInt324_2);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0xb1) : Vector512.Shuffle(v, ShuffleInt321), MaskInt324_1);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle4x128(v, v, 0xb1) : Vector512.Shuffle(v, ShuffleInt324), MaskInt328_4);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0x4e) : Vector512.Shuffle(v, ShuffleInt322), MaskInt328_2);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0xb1) : Vector512.Shuffle(v, ShuffleInt321), MaskInt328_1);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle4x128(v, v, 0x4e) : Vector512.Shuffle(v, ShuffleInt328), MaskInt3216_8);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle4x128(v, v, 0xb1) : Vector512.Shuffle(v, ShuffleInt324), MaskInt3216_4);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0x4e) : Vector512.Shuffle(v, ShuffleInt322), MaskInt3216_2);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0xb1) : Vector512.Shuffle(v, ShuffleInt321), MaskInt3216_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> MergeVectorInt32(Vector512<int> v)
    {
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle4x128(v, v, 0x4e) : Vector512.Shuffle(v, ShuffleInt328), MaskInt3216_8);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle4x128(v, v, 0xb1) : Vector512.Shuffle(v, ShuffleInt324), MaskInt3216_4);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0x4e) : Vector512.Shuffle(v, ShuffleInt322), MaskInt3216_2);
        v = ExchangeInt32(v, Avx512F.IsSupported ? Avx512F.Shuffle(v, 0xb1) : Vector512.Shuffle(v, ShuffleInt321), MaskInt3216_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> ExchangeInt32(Vector512<int> v, Vector512<int> other, Vector512<int> mask)
    {
        var lo = Vector512.Min(v, other);
        var hi = Vector512.Max(v, other);
        return Avx512F.IsSupported ? Avx512F.BlendVariable(hi, lo, mask) :
            Vector512.ConditionalSelect(mask, lo, hi);
    }

    private static readonly Vector512<ulong> ShuffleUInt641 = Vector512.Create(1UL, 0UL, 3UL, 2UL, 5UL, 4UL, 7UL, 6UL);
    private static readonly Vector512<ulong> ShuffleUInt642 = Vector512.Create(2UL, 3UL, 0UL, 1UL, 6UL, 7UL, 4UL, 5UL);
    private static readonly Vector512<ulong> ShuffleUInt644 = Vector512.Create(4UL, 5UL, 6UL, 7UL, 0UL, 1UL, 2UL, 3UL);
    private static readonly Vector512<ulong> ReverseUInt64 = Vector512.Create(7UL, 6UL, 5UL, 4UL, 3UL, 2UL, 1UL, 0UL);
    private static readonly Vector512<ulong> MaskUInt642_1 = Vector512.Create(ulong.MaxValue, 0UL, 0UL, ulong.MaxValue, ulong.MaxValue, 0UL, 0UL, ulong.MaxValue);
    private static readonly Vector512<ulong> MaskUInt644_2 = Vector512.Create(ulong.MaxValue, ulong.MaxValue, 0UL, 0UL, 0UL, 0UL, ulong.MaxValue, ulong.MaxValue);
    private static readonly Vector512<ulong> MaskUInt644_1 = Vector512.Create(ulong.MaxValue, 0UL, ulong.MaxValue, 0UL, 0UL, ulong.MaxValue, 0UL, ulong.MaxValue);
    private static readonly Vector512<ulong> MaskUInt648_4 = Vector512.Create(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, 0UL, 0UL, 0UL, 0UL);
    private static readonly Vector512<ulong> MaskUInt648_2 = Vector512.Create(ulong.MaxValue, ulong.MaxValue, 0UL, 0UL, ulong.MaxValue, ulong.MaxValue, 0UL, 0UL);
    private static readonly Vector512<ulong> MaskUInt648_1 = Vector512.Create(ulong.MaxValue, 0UL, ulong.MaxValue, 0UL, ulong.MaxValue, 0UL, ulong.MaxValue, 0UL);

    public static void SortUInt64(Span<ulong> values)
    {
        if (values.Length == 128)
        {
            Sort128UInt64(values);
            return;
        }

        int size = Math.Max(8, (int)BitOperations.RoundUpToPowerOf2((uint)values.Length));
        if (size == values.Length)
        {
            SortUInt64Core(values);
            return;
        }
        Span<ulong> padded = stackalloc ulong[size];
        values.CopyTo(padded);
        padded[values.Length..].Fill(ulong.MaxValue);
        SortUInt64Core(padded);
        padded[..values.Length].CopyTo(values);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Sort128UInt64(Span<ulong> values)
    {
        ref ulong data = ref MemoryMarshal.GetReference(values);
        var v0 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 0));
        var v1 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 8));
        var v2 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 16));
        var v3 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 24));
        var v4 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 32));
        var v5 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 40));
        var v6 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 48));
        var v7 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 56));
        var v8 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 64));
        var v9 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 72));
        var v10 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 80));
        var v11 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 88));
        var v12 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 96));
        var v13 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 104));
        var v14 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 112));
        var v15 = SortVectorUInt64(Vector512.LoadUnsafe(ref data, 120));
        var r2_0 = Vector512.Shuffle(v1, ReverseUInt64);
        var h2_0 = Vector512.Max(v0, r2_0);
        v0 = Vector512.Min(v0, r2_0);
        v1 = h2_0;
        v0 = MergeVectorUInt64(v0);
        v1 = MergeVectorUInt64(v1);
        var r2_2 = Vector512.Shuffle(v3, ReverseUInt64);
        var h2_2 = Vector512.Max(v2, r2_2);
        v2 = Vector512.Min(v2, r2_2);
        v3 = h2_2;
        v2 = MergeVectorUInt64(v2);
        v3 = MergeVectorUInt64(v3);
        var r2_4 = Vector512.Shuffle(v5, ReverseUInt64);
        var h2_4 = Vector512.Max(v4, r2_4);
        v4 = Vector512.Min(v4, r2_4);
        v5 = h2_4;
        v4 = MergeVectorUInt64(v4);
        v5 = MergeVectorUInt64(v5);
        var r2_6 = Vector512.Shuffle(v7, ReverseUInt64);
        var h2_6 = Vector512.Max(v6, r2_6);
        v6 = Vector512.Min(v6, r2_6);
        v7 = h2_6;
        v6 = MergeVectorUInt64(v6);
        v7 = MergeVectorUInt64(v7);
        var r2_8 = Vector512.Shuffle(v9, ReverseUInt64);
        var h2_8 = Vector512.Max(v8, r2_8);
        v8 = Vector512.Min(v8, r2_8);
        v9 = h2_8;
        v8 = MergeVectorUInt64(v8);
        v9 = MergeVectorUInt64(v9);
        var r2_10 = Vector512.Shuffle(v11, ReverseUInt64);
        var h2_10 = Vector512.Max(v10, r2_10);
        v10 = Vector512.Min(v10, r2_10);
        v11 = h2_10;
        v10 = MergeVectorUInt64(v10);
        v11 = MergeVectorUInt64(v11);
        var r2_12 = Vector512.Shuffle(v13, ReverseUInt64);
        var h2_12 = Vector512.Max(v12, r2_12);
        v12 = Vector512.Min(v12, r2_12);
        v13 = h2_12;
        v12 = MergeVectorUInt64(v12);
        v13 = MergeVectorUInt64(v13);
        var r2_14 = Vector512.Shuffle(v15, ReverseUInt64);
        var h2_14 = Vector512.Max(v14, r2_14);
        v14 = Vector512.Min(v14, r2_14);
        v15 = h2_14;
        v14 = MergeVectorUInt64(v14);
        v15 = MergeVectorUInt64(v15);
        var r4_0 = Vector512.Shuffle(v3, ReverseUInt64);
        var h4_0 = Vector512.Max(v0, r4_0);
        v0 = Vector512.Min(v0, r4_0);
        var r4_1 = Vector512.Shuffle(v2, ReverseUInt64);
        var h4_1 = Vector512.Max(v1, r4_1);
        v1 = Vector512.Min(v1, r4_1);
        v2 = h4_0;
        v3 = h4_1;
        var t4_1_0 = Vector512.Max(v0, v1);
        v0 = Vector512.Min(v0, v1);
        v1 = t4_1_0;
        var t4_1_2 = Vector512.Max(v2, v3);
        v2 = Vector512.Min(v2, v3);
        v3 = t4_1_2;
        v0 = MergeVectorUInt64(v0);
        v1 = MergeVectorUInt64(v1);
        v2 = MergeVectorUInt64(v2);
        v3 = MergeVectorUInt64(v3);
        var r4_4 = Vector512.Shuffle(v7, ReverseUInt64);
        var h4_4 = Vector512.Max(v4, r4_4);
        v4 = Vector512.Min(v4, r4_4);
        var r4_5 = Vector512.Shuffle(v6, ReverseUInt64);
        var h4_5 = Vector512.Max(v5, r4_5);
        v5 = Vector512.Min(v5, r4_5);
        v6 = h4_4;
        v7 = h4_5;
        var t4_1_4 = Vector512.Max(v4, v5);
        v4 = Vector512.Min(v4, v5);
        v5 = t4_1_4;
        var t4_1_6 = Vector512.Max(v6, v7);
        v6 = Vector512.Min(v6, v7);
        v7 = t4_1_6;
        v4 = MergeVectorUInt64(v4);
        v5 = MergeVectorUInt64(v5);
        v6 = MergeVectorUInt64(v6);
        v7 = MergeVectorUInt64(v7);
        var r4_8 = Vector512.Shuffle(v11, ReverseUInt64);
        var h4_8 = Vector512.Max(v8, r4_8);
        v8 = Vector512.Min(v8, r4_8);
        var r4_9 = Vector512.Shuffle(v10, ReverseUInt64);
        var h4_9 = Vector512.Max(v9, r4_9);
        v9 = Vector512.Min(v9, r4_9);
        v10 = h4_8;
        v11 = h4_9;
        var t4_1_8 = Vector512.Max(v8, v9);
        v8 = Vector512.Min(v8, v9);
        v9 = t4_1_8;
        var t4_1_10 = Vector512.Max(v10, v11);
        v10 = Vector512.Min(v10, v11);
        v11 = t4_1_10;
        v8 = MergeVectorUInt64(v8);
        v9 = MergeVectorUInt64(v9);
        v10 = MergeVectorUInt64(v10);
        v11 = MergeVectorUInt64(v11);
        var r4_12 = Vector512.Shuffle(v15, ReverseUInt64);
        var h4_12 = Vector512.Max(v12, r4_12);
        v12 = Vector512.Min(v12, r4_12);
        var r4_13 = Vector512.Shuffle(v14, ReverseUInt64);
        var h4_13 = Vector512.Max(v13, r4_13);
        v13 = Vector512.Min(v13, r4_13);
        v14 = h4_12;
        v15 = h4_13;
        var t4_1_12 = Vector512.Max(v12, v13);
        v12 = Vector512.Min(v12, v13);
        v13 = t4_1_12;
        var t4_1_14 = Vector512.Max(v14, v15);
        v14 = Vector512.Min(v14, v15);
        v15 = t4_1_14;
        v12 = MergeVectorUInt64(v12);
        v13 = MergeVectorUInt64(v13);
        v14 = MergeVectorUInt64(v14);
        v15 = MergeVectorUInt64(v15);
        var r8_0 = Vector512.Shuffle(v7, ReverseUInt64);
        var h8_0 = Vector512.Max(v0, r8_0);
        v0 = Vector512.Min(v0, r8_0);
        var r8_1 = Vector512.Shuffle(v6, ReverseUInt64);
        var h8_1 = Vector512.Max(v1, r8_1);
        v1 = Vector512.Min(v1, r8_1);
        var r8_2 = Vector512.Shuffle(v5, ReverseUInt64);
        var h8_2 = Vector512.Max(v2, r8_2);
        v2 = Vector512.Min(v2, r8_2);
        var r8_3 = Vector512.Shuffle(v4, ReverseUInt64);
        var h8_3 = Vector512.Max(v3, r8_3);
        v3 = Vector512.Min(v3, r8_3);
        v4 = h8_0;
        v5 = h8_1;
        v6 = h8_2;
        v7 = h8_3;
        var t8_2_0 = Vector512.Max(v0, v2);
        v0 = Vector512.Min(v0, v2);
        v2 = t8_2_0;
        var t8_2_1 = Vector512.Max(v1, v3);
        v1 = Vector512.Min(v1, v3);
        v3 = t8_2_1;
        var t8_2_4 = Vector512.Max(v4, v6);
        v4 = Vector512.Min(v4, v6);
        v6 = t8_2_4;
        var t8_2_5 = Vector512.Max(v5, v7);
        v5 = Vector512.Min(v5, v7);
        v7 = t8_2_5;
        var t8_1_0 = Vector512.Max(v0, v1);
        v0 = Vector512.Min(v0, v1);
        v1 = t8_1_0;
        var t8_1_2 = Vector512.Max(v2, v3);
        v2 = Vector512.Min(v2, v3);
        v3 = t8_1_2;
        var t8_1_4 = Vector512.Max(v4, v5);
        v4 = Vector512.Min(v4, v5);
        v5 = t8_1_4;
        var t8_1_6 = Vector512.Max(v6, v7);
        v6 = Vector512.Min(v6, v7);
        v7 = t8_1_6;
        v0 = MergeVectorUInt64(v0);
        v1 = MergeVectorUInt64(v1);
        v2 = MergeVectorUInt64(v2);
        v3 = MergeVectorUInt64(v3);
        v4 = MergeVectorUInt64(v4);
        v5 = MergeVectorUInt64(v5);
        v6 = MergeVectorUInt64(v6);
        v7 = MergeVectorUInt64(v7);
        var r8_8 = Vector512.Shuffle(v15, ReverseUInt64);
        var h8_8 = Vector512.Max(v8, r8_8);
        v8 = Vector512.Min(v8, r8_8);
        var r8_9 = Vector512.Shuffle(v14, ReverseUInt64);
        var h8_9 = Vector512.Max(v9, r8_9);
        v9 = Vector512.Min(v9, r8_9);
        var r8_10 = Vector512.Shuffle(v13, ReverseUInt64);
        var h8_10 = Vector512.Max(v10, r8_10);
        v10 = Vector512.Min(v10, r8_10);
        var r8_11 = Vector512.Shuffle(v12, ReverseUInt64);
        var h8_11 = Vector512.Max(v11, r8_11);
        v11 = Vector512.Min(v11, r8_11);
        v12 = h8_8;
        v13 = h8_9;
        v14 = h8_10;
        v15 = h8_11;
        var t8_2_8 = Vector512.Max(v8, v10);
        v8 = Vector512.Min(v8, v10);
        v10 = t8_2_8;
        var t8_2_9 = Vector512.Max(v9, v11);
        v9 = Vector512.Min(v9, v11);
        v11 = t8_2_9;
        var t8_2_12 = Vector512.Max(v12, v14);
        v12 = Vector512.Min(v12, v14);
        v14 = t8_2_12;
        var t8_2_13 = Vector512.Max(v13, v15);
        v13 = Vector512.Min(v13, v15);
        v15 = t8_2_13;
        var t8_1_8 = Vector512.Max(v8, v9);
        v8 = Vector512.Min(v8, v9);
        v9 = t8_1_8;
        var t8_1_10 = Vector512.Max(v10, v11);
        v10 = Vector512.Min(v10, v11);
        v11 = t8_1_10;
        var t8_1_12 = Vector512.Max(v12, v13);
        v12 = Vector512.Min(v12, v13);
        v13 = t8_1_12;
        var t8_1_14 = Vector512.Max(v14, v15);
        v14 = Vector512.Min(v14, v15);
        v15 = t8_1_14;
        v8 = MergeVectorUInt64(v8);
        v9 = MergeVectorUInt64(v9);
        v10 = MergeVectorUInt64(v10);
        v11 = MergeVectorUInt64(v11);
        v12 = MergeVectorUInt64(v12);
        v13 = MergeVectorUInt64(v13);
        v14 = MergeVectorUInt64(v14);
        v15 = MergeVectorUInt64(v15);
        var r16_0 = Vector512.Shuffle(v15, ReverseUInt64);
        var h16_0 = Vector512.Max(v0, r16_0);
        v0 = Vector512.Min(v0, r16_0);
        var r16_1 = Vector512.Shuffle(v14, ReverseUInt64);
        var h16_1 = Vector512.Max(v1, r16_1);
        v1 = Vector512.Min(v1, r16_1);
        var r16_2 = Vector512.Shuffle(v13, ReverseUInt64);
        var h16_2 = Vector512.Max(v2, r16_2);
        v2 = Vector512.Min(v2, r16_2);
        var r16_3 = Vector512.Shuffle(v12, ReverseUInt64);
        var h16_3 = Vector512.Max(v3, r16_3);
        v3 = Vector512.Min(v3, r16_3);
        var r16_4 = Vector512.Shuffle(v11, ReverseUInt64);
        var h16_4 = Vector512.Max(v4, r16_4);
        v4 = Vector512.Min(v4, r16_4);
        var r16_5 = Vector512.Shuffle(v10, ReverseUInt64);
        var h16_5 = Vector512.Max(v5, r16_5);
        v5 = Vector512.Min(v5, r16_5);
        var r16_6 = Vector512.Shuffle(v9, ReverseUInt64);
        var h16_6 = Vector512.Max(v6, r16_6);
        v6 = Vector512.Min(v6, r16_6);
        var r16_7 = Vector512.Shuffle(v8, ReverseUInt64);
        var h16_7 = Vector512.Max(v7, r16_7);
        v7 = Vector512.Min(v7, r16_7);
        v8 = h16_0;
        v9 = h16_1;
        v10 = h16_2;
        v11 = h16_3;
        v12 = h16_4;
        v13 = h16_5;
        v14 = h16_6;
        v15 = h16_7;
        var t16_4_0 = Vector512.Max(v0, v4);
        v0 = Vector512.Min(v0, v4);
        v4 = t16_4_0;
        var t16_4_1 = Vector512.Max(v1, v5);
        v1 = Vector512.Min(v1, v5);
        v5 = t16_4_1;
        var t16_4_2 = Vector512.Max(v2, v6);
        v2 = Vector512.Min(v2, v6);
        v6 = t16_4_2;
        var t16_4_3 = Vector512.Max(v3, v7);
        v3 = Vector512.Min(v3, v7);
        v7 = t16_4_3;
        var t16_4_8 = Vector512.Max(v8, v12);
        v8 = Vector512.Min(v8, v12);
        v12 = t16_4_8;
        var t16_4_9 = Vector512.Max(v9, v13);
        v9 = Vector512.Min(v9, v13);
        v13 = t16_4_9;
        var t16_4_10 = Vector512.Max(v10, v14);
        v10 = Vector512.Min(v10, v14);
        v14 = t16_4_10;
        var t16_4_11 = Vector512.Max(v11, v15);
        v11 = Vector512.Min(v11, v15);
        v15 = t16_4_11;
        var t16_2_0 = Vector512.Max(v0, v2);
        v0 = Vector512.Min(v0, v2);
        v2 = t16_2_0;
        var t16_2_1 = Vector512.Max(v1, v3);
        v1 = Vector512.Min(v1, v3);
        v3 = t16_2_1;
        var t16_2_4 = Vector512.Max(v4, v6);
        v4 = Vector512.Min(v4, v6);
        v6 = t16_2_4;
        var t16_2_5 = Vector512.Max(v5, v7);
        v5 = Vector512.Min(v5, v7);
        v7 = t16_2_5;
        var t16_2_8 = Vector512.Max(v8, v10);
        v8 = Vector512.Min(v8, v10);
        v10 = t16_2_8;
        var t16_2_9 = Vector512.Max(v9, v11);
        v9 = Vector512.Min(v9, v11);
        v11 = t16_2_9;
        var t16_2_12 = Vector512.Max(v12, v14);
        v12 = Vector512.Min(v12, v14);
        v14 = t16_2_12;
        var t16_2_13 = Vector512.Max(v13, v15);
        v13 = Vector512.Min(v13, v15);
        v15 = t16_2_13;
        var t16_1_0 = Vector512.Max(v0, v1);
        v0 = Vector512.Min(v0, v1);
        v1 = t16_1_0;
        var t16_1_2 = Vector512.Max(v2, v3);
        v2 = Vector512.Min(v2, v3);
        v3 = t16_1_2;
        var t16_1_4 = Vector512.Max(v4, v5);
        v4 = Vector512.Min(v4, v5);
        v5 = t16_1_4;
        var t16_1_6 = Vector512.Max(v6, v7);
        v6 = Vector512.Min(v6, v7);
        v7 = t16_1_6;
        var t16_1_8 = Vector512.Max(v8, v9);
        v8 = Vector512.Min(v8, v9);
        v9 = t16_1_8;
        var t16_1_10 = Vector512.Max(v10, v11);
        v10 = Vector512.Min(v10, v11);
        v11 = t16_1_10;
        var t16_1_12 = Vector512.Max(v12, v13);
        v12 = Vector512.Min(v12, v13);
        v13 = t16_1_12;
        var t16_1_14 = Vector512.Max(v14, v15);
        v14 = Vector512.Min(v14, v15);
        v15 = t16_1_14;
        v0 = MergeVectorUInt64(v0);
        v1 = MergeVectorUInt64(v1);
        v2 = MergeVectorUInt64(v2);
        v3 = MergeVectorUInt64(v3);
        v4 = MergeVectorUInt64(v4);
        v5 = MergeVectorUInt64(v5);
        v6 = MergeVectorUInt64(v6);
        v7 = MergeVectorUInt64(v7);
        v8 = MergeVectorUInt64(v8);
        v9 = MergeVectorUInt64(v9);
        v10 = MergeVectorUInt64(v10);
        v11 = MergeVectorUInt64(v11);
        v12 = MergeVectorUInt64(v12);
        v13 = MergeVectorUInt64(v13);
        v14 = MergeVectorUInt64(v14);
        v15 = MergeVectorUInt64(v15);
        v0.StoreUnsafe(ref data, 0);
        v1.StoreUnsafe(ref data, 8);
        v2.StoreUnsafe(ref data, 16);
        v3.StoreUnsafe(ref data, 24);
        v4.StoreUnsafe(ref data, 32);
        v5.StoreUnsafe(ref data, 40);
        v6.StoreUnsafe(ref data, 48);
        v7.StoreUnsafe(ref data, 56);
        v8.StoreUnsafe(ref data, 64);
        v9.StoreUnsafe(ref data, 72);
        v10.StoreUnsafe(ref data, 80);
        v11.StoreUnsafe(ref data, 88);
        v12.StoreUnsafe(ref data, 96);
        v13.StoreUnsafe(ref data, 104);
        v14.StoreUnsafe(ref data, 112);
        v15.StoreUnsafe(ref data, 120);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SortUInt64Core(Span<ulong> values)
    {
        ref ulong data = ref MemoryMarshal.GetReference(values);
        const int lanes = 8;
        for (int i = 0; i < values.Length; i += lanes)
            SortVectorUInt64(Vector512.LoadUnsafe(ref data, (nuint)i)).StoreUnsafe(ref data, (nuint)i);
        for (int width = lanes * 2; width <= values.Length; width *= 2)
        {
            int half = width / 2;
            for (int start = 0; start < values.Length; start += width)
            {
                for (int i = 0; i < half; i += lanes)
                {
                    nuint a = (nuint)(start + i), b = (nuint)(start + width - lanes - i);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.Shuffle(Vector512.LoadUnsafe(ref data, b), ReverseUInt64);
                    Vector512.Min(left, right).StoreUnsafe(ref data, a);
                    Vector512.Max(left, right).StoreUnsafe(ref data, b);
                }
                // The upper half is now in reversed block order. Reverse it back
                // to make both halves bitonic in the same orientation.
                for (int i = 0; i < half / 2; i += lanes)
                {
                    nuint a = (nuint)(start + half + i), b = (nuint)(start + width - lanes - i);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.LoadUnsafe(ref data, b);
                    right.StoreUnsafe(ref data, a);
                    left.StoreUnsafe(ref data, b);
                }
                for (int distance = half / 2; distance >= lanes; distance /= 2)
                    for (int i = 0; i < width; i += lanes)
                    {
                        if ((i & distance) != 0) continue;
                        nuint a = (nuint)(start + i), b = (nuint)(start + i + distance);
                        var left = Vector512.LoadUnsafe(ref data, a);
                        var right = Vector512.LoadUnsafe(ref data, b);
                        Vector512.Min(left, right).StoreUnsafe(ref data, a);
                        Vector512.Max(left, right).StoreUnsafe(ref data, b);
                    }
                for (int i = 0; i < width; i += lanes)
                {
                    nuint offset = (nuint)(start + i);
                    MergeVectorUInt64(Vector512.LoadUnsafe(ref data, offset)).StoreUnsafe(ref data, offset);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> SortVectorUInt64(Vector512<ulong> v)
    {
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Permute4x64(v, 0xb1) : Vector512.Shuffle(v, ShuffleUInt641), MaskUInt642_1);
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Permute4x64(v, 0x4e) : Vector512.Shuffle(v, ShuffleUInt642), MaskUInt644_2);
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Permute4x64(v, 0xb1) : Vector512.Shuffle(v, ShuffleUInt641), MaskUInt644_1);
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Shuffle4x128(v, v, 0x4e) : Vector512.Shuffle(v, ShuffleUInt644), MaskUInt648_4);
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Permute4x64(v, 0x4e) : Vector512.Shuffle(v, ShuffleUInt642), MaskUInt648_2);
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Permute4x64(v, 0xb1) : Vector512.Shuffle(v, ShuffleUInt641), MaskUInt648_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> MergeVectorUInt64(Vector512<ulong> v)
    {
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Shuffle4x128(v, v, 0x4e) : Vector512.Shuffle(v, ShuffleUInt644), MaskUInt648_4);
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Permute4x64(v, 0x4e) : Vector512.Shuffle(v, ShuffleUInt642), MaskUInt648_2);
        v = ExchangeUInt64(v, Avx512F.IsSupported ? Avx512F.Permute4x64(v, 0xb1) : Vector512.Shuffle(v, ShuffleUInt641), MaskUInt648_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> ExchangeUInt64(Vector512<ulong> v, Vector512<ulong> other, Vector512<ulong> mask)
    {
        var lo = Vector512.Min(v, other);
        var hi = Vector512.Max(v, other);
        return Avx512F.IsSupported ? Avx512F.BlendVariable(hi, lo, mask) :
            Vector512.ConditionalSelect(mask, lo, hi);
    }

}
