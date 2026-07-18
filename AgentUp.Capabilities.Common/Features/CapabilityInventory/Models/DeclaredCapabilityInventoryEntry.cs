namespace AgentUp.Capabilities.Common.Features.CapabilityInventory.Models;

public sealed record DeclaredCapabilityInventoryEntry(
    string Id,
    IReadOnlyList<string> Versions);
