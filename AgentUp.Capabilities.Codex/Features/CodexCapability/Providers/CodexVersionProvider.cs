using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;

public sealed class CodexVersionProvider(CapabilityCliLocator locator) : ICodexVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory = new();

    public static IReadOnlyList<CapabilityCliCandidate> Candidates { get; } =
    [
        new("codex-acp", ["--version"], [])
    ];

    public static IReadOnlyList<CapabilityPackageProbe> PackageProbes { get; } =
    [
        new("brew", ["list", "--versions", "codex"], "brew:codex", "codex", "macos"),
        new("winget", ["list", "--id", "OpenAI.Codex", "--exact"], "winget:OpenAI.Codex", "OpenAI.Codex", "windows")
    ];

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        discovered.AddRange(await _inventory.LoadAsync("codex", cancellationToken));
        discovered.AddRange(await locator.DiscoverAsync("codex", Candidates, PackageProbes, cancellationToken));
        return discovered;
    }

    public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
        locator.ResolveLaunch(Candidates, installedVersions);
}
