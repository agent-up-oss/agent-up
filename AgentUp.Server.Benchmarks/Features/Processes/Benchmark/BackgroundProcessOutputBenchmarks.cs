using AgentUp.Server.Benchmarks.Fake;
using AgentUp.Server.Features.Processes.Services;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Server.Benchmarks.Features.Processes.Benchmark;

[MemoryDiagnoser]
public class BackgroundProcessOutputBenchmarks
{
    private readonly ProcessOutputService _output = new(new InMemoryOutputRepository());

    [Benchmark(OperationsPerInvoke = 1_000)]
    public async Task AppendThousandBackgroundProcessLines()
    {
        for (var index = 0; index < 1_000; index++)
            await _output.AppendAsync("workspace", "application", $"background output {index}");
    }
}
