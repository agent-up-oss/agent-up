namespace AgentUp.InstallerApp.Features.Capabilities.Models;

public sealed record CapabilityCatalogEntry(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<CapabilityArtifact> Versions);
