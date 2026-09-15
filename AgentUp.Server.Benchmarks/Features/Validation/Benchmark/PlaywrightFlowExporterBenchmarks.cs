using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Server.Benchmarks.Features.Validation.Benchmark;

[MemoryDiagnoser]
public class PlaywrightFlowExporterBenchmarks
{
    private readonly PlaywrightFlowExporter _exporter = new();
    private readonly ValidationFlow _flow = CreateFlow();

    [Benchmark]
    public PlaywrightExport ExportHundredStepFlow() => _exporter.Export(_flow);

    private static ValidationFlow CreateFlow() => new(
        "checkout", "workspace", "web", "Checkout", "Checkout validation", "/cart",
        [new ValidationAssertion(ValidationExpectation.Title, "Cart")],
        Enumerable.Range(0, 100).Select(index => new ValidationStep(
            $"step-{index}", $"Fill field {index}", ValidationAction.Fill,
            new ValidationTarget(Label: $"Field {index}"), $"Value {index}",
            [new ValidationAssertion(ValidationExpectation.Text, $"Value {index}")])).ToArray(),
        DateTimeOffset.UnixEpoch);
}
