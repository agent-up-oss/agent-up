using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;

public sealed class CodexVersionProvider(CapabilityCliLocator locator) : ICodexVersionProvider
{
    private readonly CapabilityInventoryFileProvider _inventory = new();
    private IReadOnlyList<CapabilityCliCandidate> _candidates = [];

    public async Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
    {
        _candidates = await CandidatesAsync(cancellationToken);
        if (_candidates.Count == 0)
            return [];

        return await locator.DiscoverAsync("codex", _candidates, [], cancellationToken);
    }

    public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
        locator.ResolveLaunch(_candidates, installedVersions);

    private async Task<IReadOnlyList<CapabilityCliCandidate>> CandidatesAsync(CancellationToken cancellationToken)
    {
        var entry = (await _inventory.LoadAllAsync(cancellationToken))
            .FirstOrDefault(item => item.Id.Equals("codex", StringComparison.OrdinalIgnoreCase));
        return CapabilityCliCandidateFactory.FromDeclaredCommand(entry?.Command, entry?.Arguments, entry?.VersionArguments);
    }
}
