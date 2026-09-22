namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityRegistryIndex
{
    public string SchemaVersion { get; init; } = "1";
    public IReadOnlyList<CapabilityRegistryIndexEntry> Packages { get; init; } = [];
}
