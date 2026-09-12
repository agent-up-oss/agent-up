using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Interfaces;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Codex.Tests.Features.CodexCapability.Unit;

[TestFixture]
public sealed class CodexCapabilityAdapterTests
{
    [Test]
    public async Task ValidateAsync_requiresDiscoveredCli()
    {
        var adapter = new CodexCapabilityAdapter(new FakeCodexVersionProvider());
        var result = await adapter.ValidateAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single().Code, Is.EqualTo("codex.cli.missing"));
    }

    [Test]
    public async Task ValidateAsync_succeedsWhenCliIsDiscovered()
    {
        var adapter = new CodexCapabilityAdapter(new FakeCodexVersionProvider("/usr/bin/codex-acp"));
        var result = await adapter.ValidateAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.True);
    }

    [Test]
    public async Task CreateLaunchPlanAsync_usesResolvedAcpCommand()
    {
        var adapter = new CodexCapabilityAdapter(new FakeCodexVersionProvider("/usr/bin/codex-acp"));
        var plan = await adapter.CreateLaunchPlanAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Command, Is.EqualTo("/usr/bin/codex-acp"));
            Assert.That(plan.Arguments, Is.Empty);
        });
    }

    [Test]
    public void Descriptor_isFirstPartyCodex()
    {
        Assert.That(new CodexCapabilityAdapter(new FakeCodexVersionProvider()).Descriptor.Id, Is.EqualTo("codex"));
    }

    private static CapabilityDeclaration Declaration() =>
        new("workspace-agent", "codex", new Dictionary<string, string>(), new Dictionary<string, string>());

    private sealed class FakeCodexVersionProvider(string? location = null, IReadOnlyList<string>? arguments = null) : ICodexVersionProvider
    {
        public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CapabilityInstalledVersion>>(location is null
                ? []
                : [new("codex", "1.0.0", location, CapabilityVersionSource.System, false)]);

        public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
            new(location ?? "codex-acp", arguments ?? []);
    }
}
