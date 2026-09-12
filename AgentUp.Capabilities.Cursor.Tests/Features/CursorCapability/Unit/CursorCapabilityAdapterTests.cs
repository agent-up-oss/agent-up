using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Interfaces;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Unit;

[TestFixture]
public sealed class CursorCapabilityAdapterTests
{
    [Test]
    public async Task ValidateAsync_requiresDiscoveredCli()
    {
        var adapter = new CursorCapabilityAdapter(new FakeCursorVersionProvider());
        var result = await adapter.ValidateAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single().Code, Is.EqualTo("cursor.cli.missing"));
    }

    [Test]
    public async Task ValidateAsync_succeedsWhenCliIsDiscovered()
    {
        var adapter = new CursorCapabilityAdapter(new FakeCursorVersionProvider("/home/dev/.local/bin/agent", ["acp"]));
        var result = await adapter.ValidateAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.True);
    }

    [Test]
    public async Task CreateLaunchPlanAsync_usesAgentAcp()
    {
        var adapter = new CursorCapabilityAdapter(new FakeCursorVersionProvider("/home/dev/.local/bin/agent", ["acp"]));
        var plan = await adapter.CreateLaunchPlanAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Command, Is.EqualTo("/home/dev/.local/bin/agent"));
            Assert.That(plan.Arguments, Is.EqualTo(new[] { "acp" }));
        });
    }

    [Test]
    public void Descriptor_isFirstPartyCursor()
    {
        Assert.That(new CursorCapabilityAdapter(new FakeCursorVersionProvider()).Descriptor.Id, Is.EqualTo("cursor"));
    }

    private static CapabilityDeclaration Declaration() =>
        new("workspace-agent", "cursor", new Dictionary<string, string>(), new Dictionary<string, string>());

    private sealed class FakeCursorVersionProvider(string? location = null, IReadOnlyList<string>? arguments = null) : ICursorVersionProvider
    {
        public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CapabilityInstalledVersion>>(location is null
                ? []
                : [new("cursor", "1.0.0", location, CapabilityVersionSource.System, false)]);

        public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
            new(location ?? "agent", arguments ?? ["acp"]);
    }
}
