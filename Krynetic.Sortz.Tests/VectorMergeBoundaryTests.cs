using Xunit;

namespace Krynetic.Sortz.Tests;

public sealed class VectorMergeBoundaryTests
{
    [Theory]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(511)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(2048)]
    [InlineData(10000)]
    [InlineData(16384)]
    [InlineData(16385)]
    public void NumericSortsPreserveValuesAndSliceBounds(int count)
    {
        var random = new Random(count);
        for (int pattern = 0; pattern < 6; pattern++)
        {
            int[] input = new int[count];
            ulong[] wide = new ulong[count];
            ushort[] narrow = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                int x = pattern switch
                {
                    0 => (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1),
                    1 => i % 2 == 0 ? int.MinValue : int.MaxValue,
                    2 => random.Next(5),
                    3 => i < count / 2 ? i : count - i,
                    4 => i % 97 == 0 ? int.MinValue : 7,
                    _ => random.Next(-1024, 1024)
                };
                input[i] = x;
                wide[i] = ((ulong)(uint)x << 32) | (uint)x;
                narrow[i] = unchecked((ushort)x);
            }

            Check(input);
            Check(wide);
            Check(narrow);
            Check(Array.ConvertAll(input, x => unchecked((uint)x)));
            Check(Array.ConvertAll(wide, x => unchecked((long)x)));
            Check(Array.ConvertAll(narrow, x => unchecked((short)x)));
        }
    }

    private static void Check<T>(T[] input)
    {
        T[] expected = (T[])input.Clone();
        Array.Sort(expected);
        T[] padded = new T[input.Length + 2];
        padded[0] = input[^1];
        padded[^1] = input[0];
        input.CopyTo(padded, 1);
        Sortz.Sort(padded.AsSpan(1, input.Length));
        Assert.Equal(expected, padded[1..^1]);
        Assert.Equal(input[^1], padded[0]);
        Assert.Equal(input[0], padded[^1]);
    }
}
