#if !NETSTANDARD2_1
using System.Runtime.InteropServices;
#endif

namespace Krynetic.Sortz;

public static class SortzExtensions
{
    public static void Sortz<T>(this List<T> list, IComparer<T>? comparer = null)
    {
#if NETSTANDARD2_1
        if (list is null)
            throw new ArgumentNullException(nameof(list));

        // .NET Standard does not expose the backing span of List<T>.
        list.Sort(comparer);
#else
        ArgumentNullException.ThrowIfNull(list);
        global::Krynetic.Sortz.Sortz.Sort(CollectionsMarshal.AsSpan(list), comparer);
#endif
    }
}
