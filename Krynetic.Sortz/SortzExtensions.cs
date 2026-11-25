using System.Runtime.InteropServices;

namespace Krynetic.Sortz;

public static class SortzExtensions
{
    public static void Sortz<T>(this List<T> list, IComparer<T>? comparer = null)
    {
#if NET5_0
        if (list is null)
            throw new ArgumentNullException(nameof(list));
#else
        ArgumentNullException.ThrowIfNull(list);
#endif
        global::Krynetic.Sortz.Sortz.Sort(CollectionsMarshal.AsSpan(list), comparer);
    }
}
