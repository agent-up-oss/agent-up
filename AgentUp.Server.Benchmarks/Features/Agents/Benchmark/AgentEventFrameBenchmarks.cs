using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using BenchmarkDotNet.Attributes;
using System.Text.Json;

namespace AgentUp.Server.Benchmarks.Features.Agents.Benchmark;

[MemoryDiagnoser]
public class AgentEventFrameBenchmarks
{
    private readonly AgentEventFrameProvider _frames = new();
    private readonly AgentEventDto _event = new(
        1,
        "session_update",
        JsonSerializer.SerializeToElement(new { sessionUpdate = "agent_message_chunk", content = new { text = new string('x', 4096) } }),
        DateTimeOffset.UnixEpoch);

    [Benchmark]
    public string SerializeFourKilobyteAgentEvent() => _frames.Frame(_event);
}
