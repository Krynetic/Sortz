using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Sortz;

internal static class PrimitiveSimdSort
{
    private static readonly Vector512<ushort> Shuffle1 = Vector512.Create((ushort)1, (ushort)0, (ushort)3, (ushort)2, (ushort)5, (ushort)4, (ushort)7, (ushort)6, (ushort)9, (ushort)8, (ushort)11, (ushort)10, (ushort)13, (ushort)12, (ushort)15, (ushort)14, (ushort)17, (ushort)16, (ushort)19, (ushort)18, (ushort)21, (ushort)20, (ushort)23, (ushort)22, (ushort)25, (ushort)24, (ushort)27, (ushort)26, (ushort)29, (ushort)28, (ushort)31, (ushort)30);
    private static readonly Vector512<ushort> Shuffle2 = Vector512.Create((ushort)2, (ushort)3, (ushort)0, (ushort)1, (ushort)6, (ushort)7, (ushort)4, (ushort)5, (ushort)10, (ushort)11, (ushort)8, (ushort)9, (ushort)14, (ushort)15, (ushort)12, (ushort)13, (ushort)18, (ushort)19, (ushort)16, (ushort)17, (ushort)22, (ushort)23, (ushort)20, (ushort)21, (ushort)26, (ushort)27, (ushort)24, (ushort)25, (ushort)30, (ushort)31, (ushort)28, (ushort)29);
    private static readonly Vector512<ushort> Shuffle4 = Vector512.Create((ushort)4, (ushort)5, (ushort)6, (ushort)7, (ushort)0, (ushort)1, (ushort)2, (ushort)3, (ushort)12, (ushort)13, (ushort)14, (ushort)15, (ushort)8, (ushort)9, (ushort)10, (ushort)11, (ushort)20, (ushort)21, (ushort)22, (ushort)23, (ushort)16, (ushort)17, (ushort)18, (ushort)19, (ushort)28, (ushort)29, (ushort)30, (ushort)31, (ushort)24, (ushort)25, (ushort)26, (ushort)27);
    private static readonly Vector512<ushort> Shuffle8 = Vector512.Create((ushort)8, (ushort)9, (ushort)10, (ushort)11, (ushort)12, (ushort)13, (ushort)14, (ushort)15, (ushort)0, (ushort)1, (ushort)2, (ushort)3, (ushort)4, (ushort)5, (ushort)6, (ushort)7, (ushort)24, (ushort)25, (ushort)26, (ushort)27, (ushort)28, (ushort)29, (ushort)30, (ushort)31, (ushort)16, (ushort)17, (ushort)18, (ushort)19, (ushort)20, (ushort)21, (ushort)22, (ushort)23);
    private static readonly Vector512<ushort> Shuffle16 = Vector512.Create((ushort)16, (ushort)17, (ushort)18, (ushort)19, (ushort)20, (ushort)21, (ushort)22, (ushort)23, (ushort)24, (ushort)25, (ushort)26, (ushort)27, (ushort)28, (ushort)29, (ushort)30, (ushort)31, (ushort)0, (ushort)1, (ushort)2, (ushort)3, (ushort)4, (ushort)5, (ushort)6, (ushort)7, (ushort)8, (ushort)9, (ushort)10, (ushort)11, (ushort)12, (ushort)13, (ushort)14, (ushort)15);
    private static readonly Vector512<ushort> Reverse = Vector512.Create((ushort)31, (ushort)30, (ushort)29, (ushort)28, (ushort)27, (ushort)26, (ushort)25, (ushort)24, (ushort)23, (ushort)22, (ushort)21, (ushort)20, (ushort)19, (ushort)18, (ushort)17, (ushort)16, (ushort)15, (ushort)14, (ushort)13, (ushort)12, (ushort)11, (ushort)10, (ushort)9, (ushort)8, (ushort)7, (ushort)6, (ushort)5, (ushort)4, (ushort)3, (ushort)2, (ushort)1, (ushort)0);
    private static readonly Vector512<ushort> Mask2_1 = Vector512.Create(ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask4_2 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask4_1 = Vector512.Create(ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask8_4 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask8_2 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask8_1 = Vector512.Create(ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask16_8 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask16_4 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask16_2 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask16_1 = Vector512.Create(ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue);
    private static readonly Vector512<ushort> Mask32_16 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0);
    private static readonly Vector512<ushort> Mask32_8 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0, (ushort)0);
    private static readonly Vector512<ushort> Mask32_4 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, (ushort)0, (ushort)0);
    private static readonly Vector512<ushort> Mask32_2 = Vector512.Create(ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0, ushort.MaxValue, ushort.MaxValue, (ushort)0, (ushort)0);
    private static readonly Vector512<ushort> Mask32_1 = Vector512.Create(ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0, ushort.MaxValue, (ushort)0);

    public static void SortUInt16(Span<ushort> values)
    {
        int size = Math.Max(32, (int)BitOperations.RoundUpToPowerOf2((uint)values.Length));
        if (size == values.Length)
        {
            SortCore(values);
            return;
        }
        Span<ushort> padded = stackalloc ushort[size];
        values.CopyTo(padded);
        padded[values.Length..].Fill(ushort.MaxValue);
        SortCore(padded);
        padded[..values.Length].CopyTo(values);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void Sort128(Span<ushort> values)
    {
        ref ushort data = ref MemoryMarshal.GetReference(values);
        var v0 = SortVector(Vector512.LoadUnsafe(ref data, 0));
        var v1 = SortVector(Vector512.LoadUnsafe(ref data, 32));
        var v2 = SortVector(Vector512.LoadUnsafe(ref data, 64));
        var v3 = SortVector(Vector512.LoadUnsafe(ref data, 96));
        var r2_0 = Vector512.Shuffle(v1, Reverse);
        var h2_0 = Vector512.Max(v0, r2_0);
        v0 = Vector512.Min(v0, r2_0);
        v1 = h2_0;
        v0 = MergeVector(v0);
        v1 = MergeVector(v1);
        var r2_2 = Vector512.Shuffle(v3, Reverse);
        var h2_2 = Vector512.Max(v2, r2_2);
        v2 = Vector512.Min(v2, r2_2);
        v3 = h2_2;
        v2 = MergeVector(v2);
        v3 = MergeVector(v3);
        var r4_0 = Vector512.Shuffle(v3, Reverse);
        var h4_0 = Vector512.Max(v0, r4_0);
        v0 = Vector512.Min(v0, r4_0);
        var r4_1 = Vector512.Shuffle(v2, Reverse);
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
        v0 = MergeVector(v0);
        v1 = MergeVector(v1);
        v2 = MergeVector(v2);
        v3 = MergeVector(v3);
        v0.StoreUnsafe(ref data, 0);
        v1.StoreUnsafe(ref data, 32);
        v2.StoreUnsafe(ref data, 64);
        v3.StoreUnsafe(ref data, 96);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void SortCore(Span<ushort> values)
    {
        ref ushort data = ref MemoryMarshal.GetReference(values);
        const int lanes = 32;
        int initialWidth;
        if (values.Length >= 128)
        {
            for (int i = 0; i < values.Length; i += 128)
                Sort128(values.Slice(i, 128));
            initialWidth = 256;
        }
        else
        {
            for (int i = 0; i < values.Length; i += lanes)
                SortVector(Vector512.LoadUnsafe(ref data, (nuint)i)).StoreUnsafe(ref data, (nuint)i);
            initialWidth = lanes * 2;
        }
        for (int width = initialWidth; width <= values.Length; width *= 2)
        {
            int half = width / 2;
            for (int start = 0; start < values.Length; start += width)
            {
                for (int i = 0; i < half; i += lanes)
                {
                    nuint a = (nuint)(start + i), b = (nuint)(start + width - lanes - i);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.Shuffle(Vector512.LoadUnsafe(ref data, b), Reverse);
                    Vector512.Min(left, right).StoreUnsafe(ref data, a);
                    Vector512.Max(left, right).StoreUnsafe(ref data, b);
                }
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
                    MergeVector(Vector512.LoadUnsafe(ref data, offset)).StoreUnsafe(ref data, offset);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> SortVector(Vector512<ushort> v)
    {
        v = Exchange(v, Shuffle1, Mask2_1);
        v = Exchange(v, Shuffle2, Mask4_2);
        v = Exchange(v, Shuffle1, Mask4_1);
        v = Exchange(v, Shuffle4, Mask8_4);
        v = Exchange(v, Shuffle2, Mask8_2);
        v = Exchange(v, Shuffle1, Mask8_1);
        v = Exchange(v, Shuffle8, Mask16_8);
        v = Exchange(v, Shuffle4, Mask16_4);
        v = Exchange(v, Shuffle2, Mask16_2);
        v = Exchange(v, Shuffle1, Mask16_1);
        v = Exchange(v, Shuffle16, Mask32_16);
        v = Exchange(v, Shuffle8, Mask32_8);
        v = Exchange(v, Shuffle4, Mask32_4);
        v = Exchange(v, Shuffle2, Mask32_2);
        v = Exchange(v, Shuffle1, Mask32_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> MergeVector(Vector512<ushort> v)
    {
        v = Exchange(v, Shuffle16, Mask32_16);
        v = Exchange(v, Shuffle8, Mask32_8);
        v = Exchange(v, Shuffle4, Mask32_4);
        v = Exchange(v, Shuffle2, Mask32_2);
        v = Exchange(v, Shuffle1, Mask32_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> Exchange(Vector512<ushort> v, Vector512<ushort> shuffle, Vector512<ushort> mask)
    {
        var other = Vector512.Shuffle(v, shuffle);
        return Vector512.ConditionalSelect(mask, Vector512.Min(v, other), Vector512.Max(v, other));
    }
}
