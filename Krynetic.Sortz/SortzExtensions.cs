using System.Buffers;
using System.Runtime.CompilerServices;

namespace Krynetic.Sortz;

/// <summary>
/// Provides extension methods for high-performance, in-place sorting
/// using the optimized Krynetic Sortz algorithms.
/// </summary>
public static class SortzExtensions
{
    /// <inheritdoc cref="Sortz.Sort{T}(T[], IComparer{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sortz<T>(this T[] array, IComparer<T>? comparer = null) =>
        global::Krynetic.Sortz.Sortz.Sort(array, comparer);

    /// <inheritdoc cref="Sortz.Sort{T}(List{T}, IComparer{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sortz<T>(this List<T> list, IComparer<T>? comparer = null) =>
        global::Krynetic.Sortz.Sortz.Sort(list, comparer);

    /// <inheritdoc cref="Sortz.Sort{T}(Span{T}, IComparer{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sortz<T>(this Span<T> values, IComparer<T>? comparer = null) =>
        global::Krynetic.Sortz.Sortz.Sort(values, comparer);

    /// <inheritdoc cref="Sortz.Sort{T}(Memory{T}, IComparer{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sortz<T>(this Memory<T> memory, IComparer<T>? comparer = null) =>
        global::Krynetic.Sortz.Sortz.Sort(memory, comparer);

    /// <inheritdoc cref="Sortz.Sort{T}(ArraySegment{T}, IComparer{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sortz<T>(this ArraySegment<T> segment, IComparer<T>? comparer = null) =>
        global::Krynetic.Sortz.Sortz.Sort(segment, comparer);

    /// <inheritdoc cref="Sortz.Sort{T}(IMemoryOwner{T}, IComparer{T})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sortz<T>(this IMemoryOwner<T> owner, IComparer<T>? comparer = null) =>
        global::Krynetic.Sortz.Sortz.Sort(owner, comparer);
}
