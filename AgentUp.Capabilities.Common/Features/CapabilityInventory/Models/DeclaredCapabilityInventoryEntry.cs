namespace AgentUp.Capabilities.Common.Features.CapabilityInventory.Models;

public sealed record DeclaredCapabilityInventoryEntry(
    string Id,
    IReadOnlyList<string> Versions,
    string? Command = null,
    IReadOnlyList<string>? Arguments = null,
    IReadOnlyList<string>? VersionArguments = null);
