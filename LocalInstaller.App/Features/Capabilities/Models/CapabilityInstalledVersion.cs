namespace AgentUp.InstallerApp.Features.Capabilities.Models;

public sealed record CapabilityInstalledVersion(
    string CapabilityId,
    string Version,
    string Location,
    CapabilityVersionSource Source,
    bool IsManaged);
