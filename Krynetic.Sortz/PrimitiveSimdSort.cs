using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Krynetic.Sortz;

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
                if (half == lanes)
                {
                    nuint a = (nuint)start, b = (nuint)(start + half);
                    var left = Vector512.LoadUnsafe(ref data, a);
                    var right = Vector512.Shuffle(Vector512.LoadUnsafe(ref data, b), Reverse);
                    Vector512.Min(left, right).StoreUnsafe(ref data, a);
                    Vector512.Max(left, right).StoreUnsafe(ref data, b);
                }
                else
                {
                    for (int i = 0; i < half / 2; i += lanes)
                    {
                        nuint a = (nuint)(start + i), b = (nuint)(start + half - lanes - i);
                        nuint c = (nuint)(start + half + i), d = (nuint)(start + width - lanes - i);
                        var l0 = Vector512.LoadUnsafe(ref data, a);
                        var l1 = Vector512.LoadUnsafe(ref data, b);
                        var r0 = Vector512.Shuffle(Vector512.LoadUnsafe(ref data, d), Reverse);
                        var r1 = Vector512.Shuffle(Vector512.LoadUnsafe(ref data, c), Reverse);
                        Vector512.Min(l0, r0).StoreUnsafe(ref data, a);
                        Vector512.Min(l1, r1).StoreUnsafe(ref data, b);
                        Vector512.Max(l0, r0).StoreUnsafe(ref data, c);
                        Vector512.Max(l1, r1).StoreUnsafe(ref data, d);
                    }
                }
                for (int distance = half / 2; distance > lanes; distance /= 2)
                    for (int block = 0; block < width; block += distance * 2)
                        for (int i = block; i < block + distance; i += lanes)
                        {
                            nuint a = (nuint)(start + i), b = (nuint)(start + i + distance);
                            var left = Vector512.LoadUnsafe(ref data, a);
                            var right = Vector512.LoadUnsafe(ref data, b);
                            Vector512.Min(left, right).StoreUnsafe(ref data, a);
                            Vector512.Max(left, right).StoreUnsafe(ref data, b);
                        }
                // Finish the adjacent-vector stage and each lane network
                // in registers, avoiding a store/reload of both intermediates.
                if (half > lanes)
                {
                    for (int i = 0; i < width; i += lanes * 2)
                    {
                        nuint a = (nuint)(start + i), b = (nuint)(start + i + lanes);
                        var left = Vector512.LoadUnsafe(ref data, a);
                        var right = Vector512.LoadUnsafe(ref data, b);
                        MergeVector(Vector512.Min(left, right)).StoreUnsafe(ref data, a);
                        MergeVector(Vector512.Max(left, right)).StoreUnsafe(ref data, b);
                    }
                }
                else
                {
                    for (int i = 0; i < width; i += lanes)
                    {
                        nuint offset = (nuint)(start + i);
                        MergeVector(Vector512.LoadUnsafe(ref data, offset)).StoreUnsafe(ref data, offset);
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> SortVector(Vector512<ushort> v)
    {
        v = Exchange(v, Vector512.Shuffle(v, Shuffle1), Mask2_1);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle2)), Mask4_2);
        v = Exchange(v, Vector512.Shuffle(v, Shuffle1), Mask4_1);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0x4E).AsUInt16() : Vector512.Shuffle(v, Shuffle4)), Mask8_4);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle2)), Mask8_2);
        v = Exchange(v, Vector512.Shuffle(v, Shuffle1), Mask8_1);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle8)), Mask16_8);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0x4E).AsUInt16() : Vector512.Shuffle(v, Shuffle4)), Mask16_4);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle2)), Mask16_2);
        v = Exchange(v, Vector512.Shuffle(v, Shuffle1), Mask16_1);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0x4E).AsUInt16() : Vector512.Shuffle(v, Shuffle16)), Mask32_16);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle8)), Mask32_8);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0x4E).AsUInt16() : Vector512.Shuffle(v, Shuffle4)), Mask32_4);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle2)), Mask32_2);
        v = Exchange(v, Vector512.Shuffle(v, Shuffle1), Mask32_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> MergeVector(Vector512<ushort> v)
    {
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0x4E).AsUInt16() : Vector512.Shuffle(v, Shuffle16)), Mask32_16);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle8)), Mask32_8);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0x4E).AsUInt16() : Vector512.Shuffle(v, Shuffle4)), Mask32_4);
        v = Exchange(v, (Avx512F.IsSupported ? Avx512F.Shuffle(v.AsUInt32(), 0xB1).AsUInt16() : Vector512.Shuffle(v, Shuffle2)), Mask32_2);
        v = Exchange(v, Vector512.Shuffle(v, Shuffle1), Mask32_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> Exchange(Vector512<ushort> v, Vector512<ushort> other, Vector512<ushort> mask)
    {
        return Vector512.ConditionalSelect(mask, Vector512.Min(v, other), Vector512.Max(v, other));
    }

    // Native byte-width networks: no widening, narrowing or scratch buffer.
    // The masks select the lower or upper member of each comparator.
    private static readonly Vector512<byte> ByteShuffle1 = Vector512.Create((byte)1, (byte)0, (byte)3, (byte)2, (byte)5, (byte)4, (byte)7, (byte)6, (byte)9, (byte)8, (byte)11, (byte)10, (byte)13, (byte)12, (byte)15, (byte)14, (byte)1, (byte)0, (byte)3, (byte)2, (byte)5, (byte)4, (byte)7, (byte)6, (byte)9, (byte)8, (byte)11, (byte)10, (byte)13, (byte)12, (byte)15, (byte)14, (byte)1, (byte)0, (byte)3, (byte)2, (byte)5, (byte)4, (byte)7, (byte)6, (byte)9, (byte)8, (byte)11, (byte)10, (byte)13, (byte)12, (byte)15, (byte)14, (byte)1, (byte)0, (byte)3, (byte)2, (byte)5, (byte)4, (byte)7, (byte)6, (byte)9, (byte)8, (byte)11, (byte)10, (byte)13, (byte)12, (byte)15, (byte)14);
    private static readonly Vector512<byte> ByteShuffle2 = Vector512.Create((byte)2, (byte)3, (byte)0, (byte)1, (byte)6, (byte)7, (byte)4, (byte)5, (byte)10, (byte)11, (byte)8, (byte)9, (byte)14, (byte)15, (byte)12, (byte)13, (byte)2, (byte)3, (byte)0, (byte)1, (byte)6, (byte)7, (byte)4, (byte)5, (byte)10, (byte)11, (byte)8, (byte)9, (byte)14, (byte)15, (byte)12, (byte)13, (byte)2, (byte)3, (byte)0, (byte)1, (byte)6, (byte)7, (byte)4, (byte)5, (byte)10, (byte)11, (byte)8, (byte)9, (byte)14, (byte)15, (byte)12, (byte)13, (byte)2, (byte)3, (byte)0, (byte)1, (byte)6, (byte)7, (byte)4, (byte)5, (byte)10, (byte)11, (byte)8, (byte)9, (byte)14, (byte)15, (byte)12, (byte)13);
    private static readonly Vector512<byte> ByteShuffle4 = Vector512.Create((byte)4, (byte)5, (byte)6, (byte)7, (byte)0, (byte)1, (byte)2, (byte)3, (byte)12, (byte)13, (byte)14, (byte)15, (byte)8, (byte)9, (byte)10, (byte)11, (byte)4, (byte)5, (byte)6, (byte)7, (byte)0, (byte)1, (byte)2, (byte)3, (byte)12, (byte)13, (byte)14, (byte)15, (byte)8, (byte)9, (byte)10, (byte)11, (byte)4, (byte)5, (byte)6, (byte)7, (byte)0, (byte)1, (byte)2, (byte)3, (byte)12, (byte)13, (byte)14, (byte)15, (byte)8, (byte)9, (byte)10, (byte)11, (byte)4, (byte)5, (byte)6, (byte)7, (byte)0, (byte)1, (byte)2, (byte)3, (byte)12, (byte)13, (byte)14, (byte)15, (byte)8, (byte)9, (byte)10, (byte)11);
    private static readonly Vector512<byte> ByteShuffle8 = Vector512.Create((byte)8, (byte)9, (byte)10, (byte)11, (byte)12, (byte)13, (byte)14, (byte)15, (byte)0, (byte)1, (byte)2, (byte)3, (byte)4, (byte)5, (byte)6, (byte)7, (byte)8, (byte)9, (byte)10, (byte)11, (byte)12, (byte)13, (byte)14, (byte)15, (byte)0, (byte)1, (byte)2, (byte)3, (byte)4, (byte)5, (byte)6, (byte)7, (byte)8, (byte)9, (byte)10, (byte)11, (byte)12, (byte)13, (byte)14, (byte)15, (byte)0, (byte)1, (byte)2, (byte)3, (byte)4, (byte)5, (byte)6, (byte)7, (byte)8, (byte)9, (byte)10, (byte)11, (byte)12, (byte)13, (byte)14, (byte)15, (byte)0, (byte)1, (byte)2, (byte)3, (byte)4, (byte)5, (byte)6, (byte)7);
    private static readonly Vector512<byte> ByteReverse = Vector512.Create((byte)63, (byte)62, (byte)61, (byte)60, (byte)59, (byte)58, (byte)57, (byte)56, (byte)55, (byte)54, (byte)53, (byte)52, (byte)51, (byte)50, (byte)49, (byte)48, (byte)47, (byte)46, (byte)45, (byte)44, (byte)43, (byte)42, (byte)41, (byte)40, (byte)39, (byte)38, (byte)37, (byte)36, (byte)35, (byte)34, (byte)33, (byte)32, (byte)31, (byte)30, (byte)29, (byte)28, (byte)27, (byte)26, (byte)25, (byte)24, (byte)23, (byte)22, (byte)21, (byte)20, (byte)19, (byte)18, (byte)17, (byte)16, (byte)15, (byte)14, (byte)13, (byte)12, (byte)11, (byte)10, (byte)9, (byte)8, (byte)7, (byte)6, (byte)5, (byte)4, (byte)3, (byte)2, (byte)1, (byte)0);
    private static readonly Vector512<byte> ByteMask2_1 = Vector512.Create((byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255);
    private static readonly Vector512<byte> ByteMask4_2 = Vector512.Create((byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask4_1 = Vector512.Create((byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255);
    private static readonly Vector512<byte> ByteMask8_4 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask8_2 = Vector512.Create((byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask8_1 = Vector512.Create((byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255);
    private static readonly Vector512<byte> ByteMask16_8 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask16_4 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask16_2 = Vector512.Create((byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask16_1 = Vector512.Create((byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255);
    private static readonly Vector512<byte> ByteMask32_16 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask32_8 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask32_4 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask32_2 = Vector512.Create((byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255);
    private static readonly Vector512<byte> ByteMask32_1 = Vector512.Create((byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255);
    private static readonly Vector512<byte> ByteMask64_32 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0);
    private static readonly Vector512<byte> ByteMask64_16 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0);
    private static readonly Vector512<byte> ByteMask64_8 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0);
    private static readonly Vector512<byte> ByteMask64_4 = Vector512.Create((byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0, (byte)255, (byte)255, (byte)255, (byte)255, (byte)0, (byte)0, (byte)0, (byte)0);
    private static readonly Vector512<byte> ByteMask64_2 = Vector512.Create((byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0, (byte)255, (byte)255, (byte)0, (byte)0);
    private static readonly Vector512<byte> ByteMask64_1 = Vector512.Create((byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0, (byte)255, (byte)0);

    public static void SortBytes(Span<byte> values, byte sign)
    {
        ref byte data = ref MemoryMarshal.GetReference(values);
        var bias = Vector512.Create(sign);
        var a = SortByteVector(Vector512.LoadUnsafe(ref data) ^ bias);
        if (values.Length == 64)
        {
            (a ^ bias).StoreUnsafe(ref data);
            return;
        }
        var b = SortByteVector(Vector512.LoadUnsafe(ref data, 64) ^ bias);
        b = Vector512.Shuffle(b, ByteReverse);
        var lo = Vector512.Min(a, b);
        var hi = Vector512.Max(a, b);
        (MergeByteVector(lo) ^ bias).StoreUnsafe(ref data);
        (MergeByteVector(hi) ^ bias).StoreUnsafe(ref data, 64);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> SortByteVector(Vector512<byte> v)
    {
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle1), ByteMask2_1);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle2), ByteMask4_2);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle1), ByteMask4_1);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle4), ByteMask8_4);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle2), ByteMask8_2);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle1), ByteMask8_1);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle8), ByteMask16_8);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle4), ByteMask16_4);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle2), ByteMask16_2);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle1), ByteMask16_1);
        v = ExchangeByte(v, Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0xB1).AsByte(), ByteMask32_16);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle8), ByteMask32_8);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle4), ByteMask32_4);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle2), ByteMask32_2);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle1), ByteMask32_1);
        v = ExchangeByte(v, Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0x4E).AsByte(), ByteMask64_32);
        v = ExchangeByte(v, Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0xB1).AsByte(), ByteMask64_16);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle8), ByteMask64_8);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle4), ByteMask64_4);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle2), ByteMask64_2);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle1), ByteMask64_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> MergeByteVector(Vector512<byte> v)
    {
        v = ExchangeByte(v, Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0x4E).AsByte(), ByteMask64_32);
        v = ExchangeByte(v, Avx512F.Shuffle4x128(v.AsUInt64(), v.AsUInt64(), 0xB1).AsByte(), ByteMask64_16);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle8), ByteMask64_8);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle4), ByteMask64_4);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle2), ByteMask64_2);
        v = ExchangeByte(v, Avx512BW.Shuffle(v, ByteShuffle1), ByteMask64_1);
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<byte> ExchangeByte(Vector512<byte> v, Vector512<byte> other, Vector512<byte> mask)
        => Vector512.ConditionalSelect(mask, Vector512.Min(v, other), Vector512.Max(v, other));
}
