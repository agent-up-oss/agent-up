using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;

namespace AgentUp.Capabilities.Common.Features.CapabilityDistribution.Services;

public sealed class CapabilityInstallPlanner(CapabilityToolCacheLayout layout)
{
    public CapabilityInstallPlan Plan(CapabilityArtifact artifact) =>
        new(
            artifact,
            layout.GetDownloadPath(artifact),
            layout.GetInstallDirectory(artifact),
            layout.GetRegistrationPath(artifact));
}
