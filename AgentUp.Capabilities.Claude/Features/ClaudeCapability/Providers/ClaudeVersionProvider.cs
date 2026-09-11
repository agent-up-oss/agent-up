using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

namespace AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;

public sealed class ClaudeVersionProvider(CapabilityCliLocator locator) : IClaudeVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory = new();

    public static IReadOnlyList<CapabilityCliCandidate> Candidates { get; } =
    [
        new("claude-agent-acp", ["--version"], []),
        new("claude-code-acp", ["--version"], [])
    ];

    public static IReadOnlyList<CapabilityPackageProbe> PackageProbes { get; } =
    [
        new("brew", ["list", "--versions", "claude-code"], "brew:claude-code", "claude-code", "macos"),
        new("winget", ["list", "--id", "Anthropic.ClaudeCode", "--exact"], "winget:Anthropic.ClaudeCode", "Anthropic.ClaudeCode", "windows")
    ];

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        var discovered = new List<CapabilityInstalledVersion>();
        discovered.AddRange(await _inventory.LoadAsync("claude", cancellationToken));
        discovered.AddRange(await locator.DiscoverAsync("claude", Candidates, PackageProbes, cancellationToken));
        return discovered;
    }

    public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
        locator.ResolveLaunch(Candidates, installedVersions);
}
