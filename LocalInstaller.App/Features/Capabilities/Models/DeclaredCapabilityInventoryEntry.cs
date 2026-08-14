namespace LocalInstaller.App.Features.Capabilities.Models;

public sealed record DeclaredCapabilityInventoryEntry(
    string Id,
    IReadOnlyList<string> Versions);
