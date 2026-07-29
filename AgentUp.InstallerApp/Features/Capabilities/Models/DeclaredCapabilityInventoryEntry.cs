namespace AgentUp.InstallerApp.Features.Capabilities.Models;

public sealed record DeclaredCapabilityInventoryEntry(
    string Id,
    IReadOnlyList<string> Versions);
