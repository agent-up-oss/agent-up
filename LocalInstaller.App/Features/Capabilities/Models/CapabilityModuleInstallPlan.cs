namespace LocalInstaller.App.Features.Capabilities.Models;

public sealed record CapabilityModuleInstallPlan(
    CapabilityArtifact Artifact,
    string DownloadPath,
    string InstallDirectory,
    string RegistrationPath);
