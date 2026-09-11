using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Interfaces;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Unit;

[TestFixture]
public sealed class ClaudeCapabilityAdapterTests
{
    [Test]
    public async Task ValidateAsync_requiresDiscoveredCli()
    {
        var adapter = new ClaudeCapabilityAdapter(new FakeClaudeVersionProvider());
        var result = await adapter.ValidateAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single().Code, Is.EqualTo("claude.cli.missing"));
    }

    [Test]
    public async Task CreateLaunchPlanAsync_usesResolvedAcpCommand()
    {
        var adapter = new ClaudeCapabilityAdapter(new FakeClaudeVersionProvider("claude-agent-acp", []));
        var plan = await adapter.CreateLaunchPlanAsync(Declaration(), await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(plan.Command, Is.EqualTo("claude-agent-acp"));
            Assert.That(plan.Arguments, Is.Empty);
        });
    }

    [Test]
    public void Descriptor_isFirstPartyClaude()
    {
        Assert.That(new ClaudeCapabilityAdapter(new FakeClaudeVersionProvider()).Descriptor.Id, Is.EqualTo("claude"));
    }

    private static CapabilityDeclaration Declaration() =>
        new("workspace-agent", "claude", new Dictionary<string, string>(), new Dictionary<string, string>());

    private sealed class FakeClaudeVersionProvider(string? location = null, IReadOnlyList<string>? arguments = null) : IClaudeVersionProvider
    {
        public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CapabilityInstalledVersion>>(location is null
                ? []
                : [new("claude", "1.0.0", location, CapabilityVersionSource.System, false)]);

        public CapabilityCliLaunch ResolveLaunch(IReadOnlyList<CapabilityInstalledVersion> installedVersions) =>
            new(location ?? "claude-agent-acp", arguments ?? []);
    }
}
