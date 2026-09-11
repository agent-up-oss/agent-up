using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Interfaces;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;

public sealed class CursorVersionProvider(CapabilityCliLocator locator) : ICursorVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory = new();

    public static IReadOnlyList<CapabilityCliCandidate> Candidates { get; } =
    [
        new("agent", ["--version"], ["acp"]),
        new("cursor-agent", ["--version"], ["acp"])
    ];

    public static IReadOnlyList<CapabilityPackageProbe> PackageProbes { get; } =
    [
        new("brew", ["list", "--versions", "cursor-cli"], "brew:cursor-cli", "cursor-cli", "macos")
    ];

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        discovered.AddRange(await _inventory.LoadAsync("cursor", cancellationToken));
        discovered.AddRange(await locator.DiscoverAsync("cursor", Candidates, PackageProbes, cancellationToken));
        return discovered;
    }

    public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
        locator.ResolveLaunch(Candidates, installedVersions);
}
