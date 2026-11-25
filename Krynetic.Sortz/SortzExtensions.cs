using System.Runtime.InteropServices;

namespace Krynetic.Sortz;

public static class SortzExtensions
{
    extension<T>(List<T> list)
    {
        public void Sortz(IComparer<T>? comparer = null)
        {
            ArgumentNullException.ThrowIfNull(list);
            global::Krynetic.Sortz.Sortz.Sort(CollectionsMarshal.AsSpan(list), comparer);
        }
    }
}
