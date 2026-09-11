using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

namespace AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;

public sealed class ClaudeVersionProvider(CapabilityCliLocator locator) : IClaudeVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory = new();
    private IReadOnlyList<CapabilityCliCandidate> _candidates = [];

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        _candidates = await CandidatesAsync(cancellationToken);
        if (_candidates.Count == 0)
            return [];

        return await locator.DiscoverAsync("claude", _candidates, [], cancellationToken);
    }

    public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
        locator.ResolveLaunch(_candidates, installedVersions);

    private async Task<IReadOnlyList<CapabilityCliCandidate>> CandidatesAsync(CancellationToken cancellationToken)
    {
        var entry = (await _inventory.LoadAllAsync(cancellationToken))
            .FirstOrDefault(item => item.Id.Equals("claude", StringComparison.OrdinalIgnoreCase));
        return CapabilityCliCandidateFactory.FromDeclaredCommand(entry?.Command, entry?.Arguments, entry?.VersionArguments);
    }
}
