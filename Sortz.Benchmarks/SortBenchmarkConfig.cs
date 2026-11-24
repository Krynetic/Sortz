using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Sortz.Benchmarks;

public sealed class SortBenchmarkConfig : ManualConfig
{
    public SortBenchmarkConfig()
    {
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Default)
            .WithJitTieringMode(BenchmarkDotNet.Engines.JitTieringMode.Auto)
            .WithWarmupCount(10)
            .WithLaunchCount(1)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));
    }
}
