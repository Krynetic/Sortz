using System.Diagnostics;
using Sortz;
using Xunit;
using Xunit.Abstractions;

namespace Sortz.Tests;

public sealed class SortPerformanceTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1000, 0)]
    [InlineData(10000, 0)]
    [InlineData(100000, 0)]
    [InlineData(100000, 1)]
    [InlineData(100000, 2)]
    [InlineData(100000, 3)]
    [InlineData(100000, 4)]
    [InlineData(100000, 5)]
    [InlineData(100000, 6)]
    [InlineData(1000000, 0)]
    public void Compare(int size, int pattern)
    {
        var source = SortInputs.Create(size, pattern);
        var baseline = new List<int>(source);
        var next = new List<int>(source);
        for (int i = 0; i < 5; i++)
        {
            Reset(baseline, source);
            Reset(next, source);
            baseline.Sort();
            next.SortNext();
        }

        const int iterations = 11;
        var builtIn = new long[iterations];
        var candidate = new long[iterations];
        var builtInBytes = new long[iterations];
        var candidateBytes = new long[iterations];
        for (int i = 0; i < iterations; i++)
        {
            Reset(baseline, source);
            Reset(next, source);
            if ((i & 1) == 0)
            {
                (builtIn[i], builtInBytes[i]) = Measure(baseline, false);
                (candidate[i], candidateBytes[i]) = Measure(next, true);
            }
            else
            {
                (candidate[i], candidateBytes[i]) = Measure(next, true);
                (builtIn[i], builtInBytes[i]) = Measure(baseline, false);
            }
            Assert.Equal<int>(baseline, next);
        }

        Array.Sort(builtIn);
        Array.Sort(candidate);
        Array.Sort(builtInBytes);
        Array.Sort(candidateBytes);
        double baselineNs = builtIn[iterations / 2] * (1e9 / Stopwatch.Frequency);
        double nextNs = candidate[iterations / 2] * (1e9 / Stopwatch.Frequency);
        output.WriteLine($"List.Sort: {baselineNs:N0} ns, {builtInBytes[iterations / 2]} B");
        output.WriteLine($"SortNext:  {nextNs:N0} ns, {candidateBytes[iterations / 2]} B");
        output.WriteLine($"Speedup: {baselineNs / Math.Max(nextNs, 1):F3}x");
    }

    private static void Reset(List<int> list, int[] source)
    {
        source.AsSpan().CopyTo(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
    }

    private static (long Ticks, long Bytes) Measure(List<int> list, bool next)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        if (next) list.SortNext();
        else list.Sort();
        long elapsed = Stopwatch.GetTimestamp() - start;
        return (elapsed, GC.GetAllocatedBytesForCurrentThread() - bytes);
    }
}
