using AgentUp.Browser.Streaming.Models;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Browser.Streaming.Benchmarks.Features.Input.Benchmark;

/// <summary>
/// Measures the JSON decoding performed for every input event in a streamed browser
/// session. Mouse movement is the high-frequency path.
/// </summary>
[MemoryDiagnoser]
public class BrowserInputParserBenchmarks
{
    private readonly BrowserInputParser _parser = new();

    [Benchmark]
    public BrowserInputCommand MouseMove()
        => _parser.Parse("""{"type":"mousemove","x":640.25,"y":360.5}""");
}
