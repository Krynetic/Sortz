using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Krynetic.Sortz.Benchmarks;

public sealed class SortBenchmarkConfig : ManualConfig
{
    public const int BatchSize = 32;

    public SortBenchmarkConfig()
    {
        AddLogger(new SummaryConsoleLogger());
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Default)
            .WithJitTieringMode(BenchmarkDotNet.Engines.JitTieringMode.Auto)
            .WithWarmupCount(30)
            .WithIterationCount(60)
            .WithLaunchCount(1)
            .WithInvocationCount(16)
            .WithUnrollFactor(16)
        );
    }
}
