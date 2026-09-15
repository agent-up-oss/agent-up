using AgentUp.Browser.Streaming.Models;
using BenchmarkDotNet.Attributes;

namespace AgentUp.Browser.Streaming.Benchmarks.Features.Input.Benchmark;

/// <summary>
/// Measures the JSON decoding performed for every input event in a streamed browser
/// session. Mouse movement is the high-frequency path; keyboard and viewport fields keep
/// the less frequent, structurally different paths visible in the same result.
/// </summary>
[MemoryDiagnoser]
public class BrowserInputParserBenchmarks
{
    private readonly BrowserInputParser _parser = new();

    [Benchmark(Baseline = true)]
    public BrowserInputCommand MouseMove()
        => _parser.Parse("""{"type":"mousemove","x":640.25,"y":360.5}""");

    [Benchmark]
    public BrowserInputCommand Click()
        => _parser.Parse("""{"type":"click","x":640,"y":360,"button":"left","clickCount":1}""");

    [Benchmark]
    public BrowserInputCommand KeyDown()
        => _parser.Parse("""{"type":"keydown","key":"Enter"}""");

    [Benchmark]
    public BrowserInputCommand ControlMode()
        => _parser.Parse("""{"type":"controlmode","width":1920,"height":1080}""");
}
