using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace Krynetic.Sortz.Benchmarks;

public sealed class SortBenchmarkConfig : ManualConfig
{
    public const int BatchSize = 8;
    public const int InvocationCount = 16;

    public SortBenchmarkConfig()
    {
        AddLogger(new SummaryConsoleLogger());
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Default)
            .WithJitTieringMode(BenchmarkDotNet.Engines.JitTieringMode.Auto)
            .WithWarmupCount(5)
            .WithIterationCount(6)
            .WithLaunchCount(1)
            .WithInvocationCount(InvocationCount)
            .WithUnrollFactor(InvocationCount)
        );
    }
}
