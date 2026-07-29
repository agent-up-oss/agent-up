namespace AgentUp.InstallerApp.Features.Capabilities.Models;

public sealed record CapabilityModuleInstallPlan(
    CapabilityArtifact Artifact,
    string DownloadPath,
    string InstallDirectory,
    string RegistrationPath);
