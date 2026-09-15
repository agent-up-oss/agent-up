using AgentUp.Server.Shared.Providers;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Server.Benchmarks.Features.Diagnostics.Benchmark;

[MemoryDiagnoser]
public class DiagnosticRedactionBenchmarks
{
    private readonly ConsoleSecretRedactor _redactor = new();
    private readonly string _line = string.Join(' ', Enumerable.Range(0, 100).Select(index => index % 10 == 0 ? $"token=secret-{index}" : $"diagnostic-{index}"));

    [Benchmark]
    public string RedactLongDiagnosticLine() => _redactor.Redact(_line);
}
