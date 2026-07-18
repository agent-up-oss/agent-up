using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Common.Features.CapabilityDistribution.Models;

public sealed record CapabilityInstallPlan(
    CapabilityArtifact Artifact,
    string DownloadPath,
    string InstallDirectory,
    string RegistrationPath);
