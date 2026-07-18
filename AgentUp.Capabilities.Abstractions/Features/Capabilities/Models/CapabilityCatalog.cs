namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityCatalog(
    string SchemaVersion,
    IReadOnlyList<CapabilityArtifact> Artifacts);
