namespace LocalInstaller.App.Features.Capabilities.Models;

public sealed record CapabilityCatalogEntry(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<CapabilityArtifact> Versions);
