using Xunit;

namespace Sortz.Tests;

public sealed class ProjectedDoubleTests
{
    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(8192)]
    [InlineData(8193)]
    [InlineData(10000)]
    public void UniformAndAdversarialFiniteRanges(int size)
    {
        var r = new Random(size);
        for (int pattern = 0; pattern < 5; pattern++)
        {
            double[] values = Enumerable.Range(0, size).Select(i => pattern switch
            {
                0 => (r.NextDouble() - .5) * 1e10,
                1 => (r.NextDouble() - .5) * double.MaxValue,
                2 => r.NextDouble() * 1e-310,
                3 => i % 5 == 0 ? double.PositiveInfinity : r.NextDouble(),
                _ => i % 100 == 0 ? r.NextDouble() : 5.0
            }).ToArray();
            double[] expected = (double[])values.Clone();
            Array.Sort(expected);
            SortzNext.Sort(values.AsSpan());
            Assert.Equal(expected, values);
        }
    }
}
