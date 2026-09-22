namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityParameterSpec
{
    public bool Required { get; init; }
    public string Type { get; init; } = "string";
}
