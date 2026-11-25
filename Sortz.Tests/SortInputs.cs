namespace Krynetic.Sortz.Tests;

internal static class SortInputs
{
    public static int[] Create(int count, int pattern, int seed = 42)
    {
        var random = new Random(seed);
        var data = new int[count];
        for (int i = 0; i < count; i++)
            data[i] = pattern switch
            {
                0 => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
                1 => i,
                2 => count - i,
                3 => random.Next(8),
                4 => 7,
                5 => Math.Min(i, count - i),
                6 => i % 31 == 0 ? random.Next() : i,
                _ => i % 2 == 0 ? int.MinValue : int.MaxValue
            };
        return data;
    }
}
