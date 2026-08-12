namespace LocalInstaller.App.Features.Capabilities.Models;

public sealed record CapabilityModuleCatalog(
    string SchemaVersion,
    IReadOnlyList<CapabilityArtifact> Artifacts);
