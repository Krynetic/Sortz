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
    [InlineData(512)]
    [InlineData(513)]
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
            Check(Array.ConvertAll(input, x => unchecked((byte)x)));
            Check(Array.ConvertAll(input, x => unchecked((sbyte)x)));
            Check(wide);
            Check(Array.ConvertAll(wide, x => BitConverter.Int64BitsToDouble(unchecked((long)x))));
            Check(Array.ConvertAll(wide, x => unchecked((nuint)x)));
            Check(Array.ConvertAll(wide, x => unchecked((nint)x)));
            Check(narrow);
            Check(Array.ConvertAll(input, x => unchecked((uint)x)));
            Check(Array.ConvertAll(wide, x => unchecked((long)x)));
            Check(Array.ConvertAll(narrow, x => unchecked((short)x)));
        }
    }

    [Theory]
    [InlineData(4096)]
    [InlineData(65535)]
    [InlineData(65536)]
    public void CompactHistogramOffsetsPreserveAllKeys(int count)
    {
        var random = new Random(count);
        var mixed = new int[count];
        var lowEntropy = new int[count];
        for (int i = 0; i < count; i++)
        {
            mixed[i] = (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
            lowEntropy[i] = (random.Next(3) - 1) << 22 | 2047;
        }
        Check(mixed);
        Check(lowEntropy);
        Check(Array.ConvertAll(mixed, x => unchecked((uint)x)));
    }

    [Theory]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(768)]
    [InlineData(1023)]
    [InlineData(1024)]
    public void StreamingMergeHandlesInterleavedAndDisjointRuns(int count)
    {
        var random = new Random(count);
        for (int pattern = 0; pattern < 5; pattern++)
        {
            var input = new int[count];
            for (int i = 0; i < count; i++)
            {
                int block = i / 128, lane = i % 128;
                input[i] = pattern switch
                {
                    0 => lane * 8 + block,
                    1 => (8 - block) * 128 + lane,
                    2 => lane < 64 ? int.MinValue : int.MaxValue,
                    3 => random.Next(-3, 4),
                    _ => random.Next(int.MinValue, int.MaxValue)
                };
            }
            Check(input);
            Check(Array.ConvertAll(input, x => unchecked((uint)x)));
            Check(Array.ConvertAll(input, x => (float)x));
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
