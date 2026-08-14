namespace LocalInstaller.Smoke.Features.PackageValidation.DTOs;

public sealed record PackageProductManifest(
    string ServiceName,
    string CliShimName,
    string ArtifactBaseName,
    string DisplayName,
    string InstallDirName,
    string WorkspaceConfigFileName = "workspace.json",
    string ServerUrlEnvironmentVariable = "LOCALINSTALLER_SERVER_URL",
    string InstallerExecutableName = "installer",
    string DesktopExecutableName = "desktop",
    string ServerExecutableName = "server",
    string CliExecutableName = "cli",
    string TrayExecutableName = "tray");
