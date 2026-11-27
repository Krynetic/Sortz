using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Krynetic.Sortz;

public static partial class Sortz
{
    /// <summary>
    /// Sorts the elements of the specified array in place.
    /// </summary>
    /// <typeparam name="T">The type of elements to sort.</typeparam>
    /// <param name="array">The array containing the elements to sort.</param>
    /// <param name="comparer">
    /// The comparer used to determine the order of elements.
    /// If <see langword="null"/>, <see cref="Comparer{T}.Default"/> is used.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="array"/> is <see langword="null"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sort<T>(T[] array, IComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(array);
        Sort(array.AsSpan(), comparer);
    }

    /// <summary>
    /// Sorts the elements of the specified list in place using highly optimized
    /// sorting algorithms specialized for the element type.
    /// </summary>
    /// <typeparam name="T">The type of elements to sort.</typeparam>
    /// <param name="list">The list containing the elements to sort.</param>
    /// <param name="comparer">
    /// The comparer used to determine the order of elements.
    /// If <see langword="null"/>, <see cref="Comparer{T}.Default"/> is used.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sort<T>(List<T> list, IComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        Sort(CollectionsMarshal.AsSpan(list), comparer);
    }

    /// <summary>
    /// Sorts the elements of the specified memory region in place.
    /// </summary>
    /// <typeparam name="T">The type of elements to sort.</typeparam>
    /// <param name="memory">The memory containing the elements to sort.</param>
    /// <param name="comparer">
    /// The comparer used to determine the order of elements.
    /// If <see langword="null"/>, <see cref="Comparer{T}.Default"/> is used.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sort<T>(Memory<T> memory, IComparer<T>? comparer = null)
    {
        Sort(memory.Span, comparer);
    }

    /// <summary>
    /// Sorts the elements of the specified array segment in place,
    /// leaving elements outside the segment unchanged.
    /// </summary>
    /// <typeparam name="T">The type of elements to sort.</typeparam>
    /// <param name="segment">The array segment containing the elements to sort.</param>
    /// <param name="comparer">
    /// The comparer used to determine the order of elements.
    /// If <see langword="null"/>, <see cref="Comparer{T}.Default"/> is used.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sort<T>(ArraySegment<T> segment, IComparer<T>? comparer = null)
    {
        Sort(segment.AsSpan(), comparer);
    }

    /// <summary>
    /// Sorts the elements of the memory region exposed by the specified
    /// memory owner in place.
    /// </summary>
    /// <typeparam name="T">The type of elements to sort.</typeparam>
    /// <param name="owner">The owner of the memory containing the elements to sort.</param>
    /// <param name="comparer">
    /// The comparer used to determine the order of elements.
    /// If <see langword="null"/>, <see cref="Comparer{T}.Default"/> is used.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="owner"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Sorts the entire exposed memory region without transferring ownership
    /// or disposing the memory owner.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sort<T>(IMemoryOwner<T> owner, IComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Sort(owner.Memory.Span, comparer);
    }
}
