using System.Numerics;
using System.Runtime.InteropServices;

namespace Sortz;

public static class SortzNext
{
    private const int InsertionThreshold = 24;

    extension<T>(List<T> list)
    {
        public void SortNext(IComparer<T>? comparer = null)
        {
            ArgumentNullException.ThrowIfNull(list);
            Sort(CollectionsMarshal.AsSpan(list), comparer);
        }
    }

    public static void Sort<T>(Span<T> values, IComparer<T>? comparer = null)
    {
        if (values.Length < 2)
            return;

        IntroSort(values, comparer ?? Comparer<T>.Default,
            2 * (BitOperations.Log2((uint)values.Length) + 1));
    }

    private static void IntroSort<T>(Span<T> values, IComparer<T> comparer, int depth)
    {
        while (values.Length > InsertionThreshold)
        {
            if (depth-- == 0)
            {
                HeapSort(values, comparer);
                return;
            }

            int last = values.Length - 1;
            int middle = last >> 1;

            if (comparer.Compare(values[0], values[middle]) > 0)
                Swap(values, 0, middle);
            if (comparer.Compare(values[0], values[last]) > 0)
                Swap(values, 0, last);
            if (comparer.Compare(values[middle], values[last]) > 0)
                Swap(values, middle, last);

            T pivot = values[middle];
            int lower = 0, current = 0, upper = last;

            while (current <= upper)
            {
                int order = comparer.Compare(values[current], pivot);
                if (order < 0)
                    Swap(values, lower++, current++);
                else if (order > 0)
                    Swap(values, current, upper--);
                else
                    current++;
            }

            // Recurse only into the smaller partition; iterate over the larger.
            Span<T> left = values[..lower];
            Span<T> right = values[(upper + 1)..];

            if (left.Length < right.Length)
            {
                IntroSort(left, comparer, depth);
                values = right;
            }
            else
            {
                IntroSort(right, comparer, depth);
                values = left;
            }
        }

        InsertionSort(values, comparer);
    }

    private static void InsertionSort<T>(Span<T> values, IComparer<T> comparer)
    {
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            int j = i;
            while (j > 0 && comparer.Compare(value, values[j - 1]) < 0)
                j--;

            if (j == i)
                continue;

            values[j..i].CopyTo(values[(j + 1)..(i + 1)]);
            values[j] = value;
        }
    }

    private static void HeapSort<T>(Span<T> values, IComparer<T> comparer)
    {
        for (int i = values.Length / 2 - 1; i >= 0; i--)
            SiftDown(values, i, comparer);

        for (int end = values.Length - 1; end > 0; end--)
        {
            Swap(values, 0, end);
            SiftDown(values[..end], 0, comparer);
        }
    }

    private static void SiftDown<T>(Span<T> values, int root, IComparer<T> comparer)
    {
        while (root < values.Length / 2)
        {
            int child = root * 2 + 1;
            if (child + 1 < values.Length &&
                comparer.Compare(values[child], values[child + 1]) < 0)
                child++;

            if (comparer.Compare(values[root], values[child]) >= 0)
                return;

            Swap(values, root, child);
            root = child;
        }
    }

    private static void Swap<T>(Span<T> values, int a, int b)
    {
        if (a != b)
            (values[a], values[b]) = (values[b], values[a]);
    }
}
