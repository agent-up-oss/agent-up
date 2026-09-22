namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityRegistryIndexEntry(
    string Id,
    string Version,
    string DisplayName,
    string Publisher,
    string Kind);
