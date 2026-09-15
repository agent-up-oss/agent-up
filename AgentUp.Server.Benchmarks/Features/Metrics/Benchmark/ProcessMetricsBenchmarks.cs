using AgentUp.Server.Features.Metrics.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Server.Benchmarks.Features.Metrics.Benchmark;

[MemoryDiagnoser]
public class ProcessMetricsBenchmarks
{
    [Benchmark]
    public Task<IReadOnlyDictionary<string, string>> SampleProcessMetrics()
        => ProcessMetricsSampler.SampleAsync(TimeSpan.Zero, CancellationToken.None);
}
