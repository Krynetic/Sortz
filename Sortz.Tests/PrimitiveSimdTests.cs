using Xunit;

namespace Sortz.Tests;

public sealed class PrimitiveSimdTests
{
    [Fact]
    public void AllSmallWidths()
    {
        var random = new Random(81);
        for (int count = 0; count <= 1024; count++)
        {
            ushort[] values = Enumerable.Range(0, count).Select(_ => (ushort)random.Next(65536)).ToArray();
            var expected = (ushort[])values.Clone();
            Array.Sort(expected);
            SortzNext.Sort(values.AsSpan());
            Assert.Equal(expected, values);
            if (count > 256) continue;
            var bytes = Enumerable.Range(0, count).Select(_ => (sbyte)random.Next(-128, 128)).ToArray();
            var sorted = (sbyte[])bytes.Clone();
            Array.Sort(sorted);
            SortzNext.Sort(bytes.AsSpan());
            Assert.Equal(sorted, bytes);
        }
    }
}
