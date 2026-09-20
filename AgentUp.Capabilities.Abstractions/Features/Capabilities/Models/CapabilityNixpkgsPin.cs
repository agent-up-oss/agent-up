namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityNixpkgsPin
{
    public string Rev { get; init; } = "";
    public string Sha256 { get; init; } = "";
}
