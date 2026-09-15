using System.Text.Json;
using AgentUp.Desktop.Features.Agents.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Desktop.Benchmarks.Features.Agents.Benchmark;

[MemoryDiagnoser]
public class AgentPresentationBenchmarks
{
    private readonly JsonDocument _plan = JsonDocument.Parse("{\"sessionUpdate\":\"plan\",\"entries\":[" + string.Join(',', Enumerable.Range(0, 100).Select(index => $"{{\"content\":\"Task {index}\",\"status\":\"in_progress\"}}")) + "]}");

    [Benchmark]
    public PresentedAgentUpdate PresentHundredEntryPlan() => AgentEventPresentationProvider.Present(_plan.RootElement);

    [Benchmark(OperationsPerInvoke = 10_000)]
    public string PresentIdleDesktopStatus()
    {
        var label = string.Empty;
        for (var index = 0; index < 10_000; index++)
            label = AgentEventPresentationProvider.ActivityLabel("idle", false, null, null, null);
        return label;
    }
}
