namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityProbeTemplate
{
    public string Command { get; init; } = "";
    public IReadOnlyList<string> Arguments { get; init; } = [];
}
