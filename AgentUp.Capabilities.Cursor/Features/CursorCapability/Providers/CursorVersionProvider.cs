using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Interfaces;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;

public sealed class CursorVersionProvider(CapabilityCliLocator locator) : ICursorVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory = new();
    private IReadOnlyList<CapabilityCliCandidate> _candidates = [];

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        _candidates = await CandidatesAsync(cancellationToken);
        if (_candidates.Count == 0)
            return [];

        return await locator.DiscoverAsync("cursor", _candidates, [], cancellationToken);
    }

    public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
        locator.ResolveLaunch(_candidates, installedVersions);

    private async Task<IReadOnlyList<CapabilityCliCandidate>> CandidatesAsync(CancellationToken cancellationToken)
    {
        var entry = (await _inventory.LoadAllAsync(cancellationToken))
            .FirstOrDefault(item => item.Id.Equals("cursor", StringComparison.OrdinalIgnoreCase));
        return CapabilityCliCandidateFactory.FromDeclaredCommand(entry?.Command, entry?.Arguments, entry?.VersionArguments);
    }
}
