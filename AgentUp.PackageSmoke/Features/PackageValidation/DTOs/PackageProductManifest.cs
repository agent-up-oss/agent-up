namespace AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

public sealed record PackageProductManifest(
    string ServiceName,
    string CliShimName,
    string ArtifactBaseName,
    string DisplayName,
    string InstallDirName,
    string WorkspaceConfigFileName = "agent-up.json");
