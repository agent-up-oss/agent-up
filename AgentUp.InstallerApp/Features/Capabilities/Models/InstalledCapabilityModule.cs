using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.InstallerApp.Features.Capabilities.Models;

public sealed record InstalledCapabilityModule(
    string Id,
    string DisplayName,
    string Description,
    string ActiveVersion,
    IReadOnlyList<CapabilityInstalledVersion> Versions);
