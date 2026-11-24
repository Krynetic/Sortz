using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

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
                    Vector512.Shuffle(Vector512.Max(left, right), ReverseInt32).StoreUnsafe(ref data, b);
                }
                // The upper half is now in reversed block order. Reverse it back
                // to make both halves bitonic in the same orientation.
                for (int i = 0; i < half / 2; i += lanes)
                {
                    nuint a = (nuint)(start + half + i), b = (nuint)(start + width - lanes - i);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.LoadUnsafe(ref data, b);
                    Vector512.Shuffle(right, ReverseInt32).StoreUnsafe(ref data, a);
                    Vector512.Shuffle(left, ReverseInt32).StoreUnsafe(ref data, b);
                }
                if (half == lanes)
                {
                    nuint b = (nuint)(start + half);
                    Vector512.Shuffle(Vector512.LoadUnsafe(ref data, b), ReverseInt32).StoreUnsafe(ref data, b);
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
        v = ExchangeInt32(v, ShuffleInt321, MaskInt322_1);
        v = ExchangeInt32(v, ShuffleInt322, MaskInt324_2);
        v = ExchangeInt32(v, ShuffleInt321, MaskInt324_1);
        v = ExchangeInt32(v, ShuffleInt324, MaskInt328_4);
        v = ExchangeInt32(v, ShuffleInt322, MaskInt328_2);
        v = ExchangeInt32(v, ShuffleInt321, MaskInt328_1);
        v = ExchangeInt32(v, ShuffleInt328, MaskInt3216_8);
        v = ExchangeInt32(v, ShuffleInt324, MaskInt3216_4);
        v = ExchangeInt32(v, ShuffleInt322, MaskInt3216_2);
        v = ExchangeInt32(v, ShuffleInt321, MaskInt3216_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> MergeVectorInt32(Vector512<int> v)
    {
        v = ExchangeInt32(v, ShuffleInt328, MaskInt3216_8);
        v = ExchangeInt32(v, ShuffleInt324, MaskInt3216_4);
        v = ExchangeInt32(v, ShuffleInt322, MaskInt3216_2);
        v = ExchangeInt32(v, ShuffleInt321, MaskInt3216_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> ExchangeInt32(Vector512<int> v, Vector512<int> shuffle, Vector512<int> mask)
    {
        var other = Vector512.Shuffle(v, shuffle);
        return Vector512.ConditionalSelect(mask, Vector512.Min(v, other), Vector512.Max(v, other));
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
                    Vector512.Shuffle(Vector512.Max(left, right), ReverseUInt64).StoreUnsafe(ref data, b);
                }
                // The upper half is now in reversed block order. Reverse it back
                // to make both halves bitonic in the same orientation.
                for (int i = 0; i < half / 2; i += lanes)
                {
                    nuint a = (nuint)(start + half + i), b = (nuint)(start + width - lanes - i);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.LoadUnsafe(ref data, b);
                    Vector512.Shuffle(right, ReverseUInt64).StoreUnsafe(ref data, a);
                    Vector512.Shuffle(left, ReverseUInt64).StoreUnsafe(ref data, b);
                }
                if (half == lanes)
                {
                    nuint b = (nuint)(start + half);
                    Vector512.Shuffle(Vector512.LoadUnsafe(ref data, b), ReverseUInt64).StoreUnsafe(ref data, b);
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
        v = ExchangeUInt64(v, ShuffleUInt641, MaskUInt642_1);
        v = ExchangeUInt64(v, ShuffleUInt642, MaskUInt644_2);
        v = ExchangeUInt64(v, ShuffleUInt641, MaskUInt644_1);
        v = ExchangeUInt64(v, ShuffleUInt644, MaskUInt648_4);
        v = ExchangeUInt64(v, ShuffleUInt642, MaskUInt648_2);
        v = ExchangeUInt64(v, ShuffleUInt641, MaskUInt648_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> MergeVectorUInt64(Vector512<ulong> v)
    {
        v = ExchangeUInt64(v, ShuffleUInt644, MaskUInt648_4);
        v = ExchangeUInt64(v, ShuffleUInt642, MaskUInt648_2);
        v = ExchangeUInt64(v, ShuffleUInt641, MaskUInt648_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ulong> ExchangeUInt64(Vector512<ulong> v, Vector512<ulong> shuffle, Vector512<ulong> mask)
    {
        var other = Vector512.Shuffle(v, shuffle);
        return Vector512.ConditionalSelect(mask, Vector512.Min(v, other), Vector512.Max(v, other));
    }

}
