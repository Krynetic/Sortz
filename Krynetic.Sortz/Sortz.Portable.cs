namespace Krynetic.Sortz;

// Portable counterpart to the modern generic introsort. No unsafe access,
// runtime-specific intrinsics, additional packages or heap scratch storage.
public static partial class Sortz
{
    private const int InsertionThreshold = 16;

    public static void Sort<T>(Span<T> values, IComparer<T>? comparer = null)
    {
        if (values.Length < 2)
            return;

        comparer ??= Comparer<T>.Default;
        if (TryOrdered(values, comparer))
            return;

        int depth = 0;
        for (int n = values.Length; n > 0; n >>= 1)
            depth += 2;

        IntroSort(values, comparer, depth);
    }

    private static bool TryOrdered<T>(Span<T> values, IComparer<T> comparer)
    {
        int i = 1, order = 0;
        while (i < values.Length && (order = comparer.Compare(values[i - 1], values[i])) == 0)
            i++;

        if (i == values.Length)
            return true;

        bool descending = order > 0;
        while (++i < values.Length)
        {
            order = comparer.Compare(values[i - 1], values[i]);
            if (descending ? order < 0 : order > 0)
                return false;
        }

        if (descending)
            for (int left = 0, right = values.Length - 1; left < right; left++, right--)
                Swap(values, left, right);

        return true;
    }

    private static void IntroSort<T, TComparer>(Span<T> values, TComparer comparer, int depth)
        where TComparer : IComparer<T>
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
            ref T firstValue = ref values[0];
            ref T middleValue = ref values[middle];
            ref T lastValue = ref values[last];

            if (comparer.Compare(firstValue, middleValue) > 0)
                (firstValue, middleValue) = (middleValue, firstValue);
            if (comparer.Compare(firstValue, lastValue) > 0)
                (firstValue, lastValue) = (lastValue, firstValue);
            if (comparer.Compare(middleValue, lastValue) > 0)
                (middleValue, lastValue) = (lastValue, middleValue);

            T pivot = middleValue;
            int lower = 1, upper = last - 1;
            while (true)
            {
                while (lower < last && comparer.Compare(values[lower], pivot) < 0)
                    lower++;
                while (upper > 0 && comparer.Compare(values[upper], pivot) > 0)
                    upper--;
                if (lower >= upper)
                    break;

                ref T a = ref values[lower++];
                ref T b = ref values[upper--];
                (a, b) = (b, a);
            }

            int split = Math.Min(Math.Max(upper + 1, 1), last);
            Span<T> left = values[..split];
            Span<T> right = values[split..];

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

    private static void InsertionSort<T, TComparer>(Span<T> values, TComparer comparer)
        where TComparer : IComparer<T>
    {
        for (int i = 1; i < values.Length; i++)
        {
            T value = values[i];
            int j = i;
            while (j > 0 && comparer.Compare(value, values[j - 1]) < 0)
            {
                values[j] = values[j - 1];
                j--;
            }

            if (j != i)
                values[j] = value;
        }
    }

    private static void HeapSort<T, TComparer>(Span<T> values, TComparer comparer)
        where TComparer : IComparer<T>
    {
        for (int i = values.Length / 2 - 1; i >= 0; i--)
            SiftDown(values, i, comparer);

        for (int end = values.Length - 1; end > 0; end--)
        {
            Swap(values, 0, end);
            SiftDown(values[..end], 0, comparer);
        }
    }

    private static void SiftDown<T, TComparer>(Span<T> values, int root, TComparer comparer)
        where TComparer : IComparer<T>
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
