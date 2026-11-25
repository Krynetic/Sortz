using System.Runtime.InteropServices;

namespace Krynetic.Sortz;

public static class SortzExtensions
{
    public static void Sortz<T>(this List<T> list, IComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(list);
        global::Krynetic.Sortz.Sortz.Sort(CollectionsMarshal.AsSpan(list), comparer);
    }
}
