using AgentUp.Server.Features.Applications.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Server.Benchmarks.Features.Applications.Benchmark;

[MemoryDiagnoser]
public class MetricsResponseParserBenchmarks
{
    private readonly string _flat = "{" + string.Join(',', Enumerable.Range(0, 100).Select(index => $"\"metric{index}\":{index}")) + "}";
    private readonly string _nested = "{\"metrics\":{\"http\":{\"requests\":12000,\"errors\":4},\"process\":{\"cpu\":12.5,\"memory\":268435456},\"healthy\":true}}";

    [Benchmark(Baseline = true)]
    public IReadOnlyDictionary<string, string> NestedApplicationMetrics() => MetricsResponseParser.Parse(_nested);

    [Benchmark]
    public IReadOnlyDictionary<string, string> HundredApplicationMetrics() => MetricsResponseParser.Parse(_flat);
}
