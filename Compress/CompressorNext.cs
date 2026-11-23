using System.Runtime.InteropServices;

namespace Compress;

public static class CompressorNext
{
    extension<T>(List<T> list)
    {
        public void SortNext(IComparer<T>? comparer = null)
        {
            ArgumentNullException.ThrowIfNull(list);
            SortCore(CollectionsMarshal.AsSpan(list), comparer);
        }
    }

    private static void SortCore<T>(Span<T> values, IComparer<T>? comparer)
    {
        values.Sort(comparer);
    }
}
