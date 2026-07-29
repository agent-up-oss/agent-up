namespace AgentUp.InstallerApp.Features.Capabilities.Models;

public sealed record CapabilityModuleCatalog(
    string SchemaVersion,
    IReadOnlyList<CapabilityArtifact> Artifacts);
