using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Models;

namespace AgentUp.PackageSmoke.Features.InstalledServiceValidation.DTOs;

public sealed record InstalledServiceProductManifest(
    string ServiceName,
    string CliShimName,
    string ArtifactBaseName,
    string DisplayName,
    string InstallDirName,
    string WorkspaceConfigFileName = "agent-up.json")
{
    internal SmokeProductConfig ToConfig()
        => new(
            ServiceName,
            CliShimName,
            ArtifactBaseName,
            DisplayName,
            InstallDirName,
            WorkspaceConfigFileName);
}
