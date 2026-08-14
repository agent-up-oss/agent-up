using LocalInstaller.App.Features.Capabilities.Models;

namespace LocalInstaller.App.Features.Capabilities.Providers;

public sealed class CapabilityModuleInstallPlanner(CapabilityModuleCacheLayout layout)
{
    public CapabilityModuleInstallPlan Plan(CapabilityArtifact artifact) =>
        new(
            artifact,
            layout.GetDownloadPath(artifact),
            layout.GetInstallDirectory(artifact),
            layout.GetRegistrationPath(artifact));
}
