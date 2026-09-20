namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityNixSpec
{
    public CapabilityNixpkgsPin? Nixpkgs { get; init; }
    public IReadOnlyList<string> Packages { get; init; } = [];
}
