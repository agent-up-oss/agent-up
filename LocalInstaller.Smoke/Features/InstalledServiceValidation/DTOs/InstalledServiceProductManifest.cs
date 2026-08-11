using LocalInstaller.Smoke.Features.InstalledServiceValidation.Models;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.DTOs;

public sealed record InstalledServiceProductManifest(
    string ServiceName,
    string CliShimName,
    string ArtifactBaseName,
    string DisplayName,
    string InstallDirName,
    string WorkspaceConfigFileName = "agent-up.json",
    string InstallerExecutableName = "installer",
    string DesktopExecutableName = "desktop",
    string ServerExecutableName = "server",
    string CliExecutableName = "cli",
    string TrayExecutableName = "tray")
{
    internal SmokeProductConfig ToConfig()
        => new(
            ServiceName,
            CliShimName,
            ArtifactBaseName,
            DisplayName,
            InstallDirName,
            WorkspaceConfigFileName,
            InstallerExecutableName,
            DesktopExecutableName,
            ServerExecutableName,
            CliExecutableName,
            TrayExecutableName);
}
