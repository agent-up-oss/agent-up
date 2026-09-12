using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Provider;

[TestFixture]
[CancelAfter(60_000)]
[Timeout(60_000)]
public sealed class ClaudeInstalledCliSmokeTests
{
    [Test]
    public async Task DiscoverAsync_usesConfiguredInventoryCommandWhenPresent()
    {
        var versions = await Adapter().DiscoverAsync(TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            foreach (var version in versions)
            {
                Assert.That(version.CapabilityId, Is.EqualTo("claude"));
                Assert.That(version.Version, Is.Not.WhiteSpace);
                Assert.That(version.Location, Is.Not.WhiteSpace);
            }
        });
    }

    [Test]
    public async Task ValidateAsync_matchesLiveDiscovery()
    {
        var adapter = Adapter();
        var installed = await adapter.DiscoverAsync(TestContext.CurrentContext.CancellationToken);
        var result = await adapter.ValidateAsync(Declaration(), installed, TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanRun, Is.EqualTo(installed.Count > 0));
            if (result.CanRun)
                return;

            Assert.That(result.Messages.Select(message => message.Code), Does.Contain("claude.cli.missing"));
        });
    }

    [Test]
    public async Task CreateLaunchPlanAsync_usesConfiguredCommandWhenInstalled()
    {
        var adapter = Adapter();
        var installed = await adapter.DiscoverAsync(TestContext.CurrentContext.CancellationToken);
        var validation = await adapter.ValidateAsync(Declaration(), installed, TestContext.CurrentContext.CancellationToken);
        if (!validation.CanRun)
        {
            Assert.That(installed, Is.Empty);
            return;
        }

        var plan = await adapter.CreateLaunchPlanAsync(Declaration(), installed, TestContext.CurrentContext.CancellationToken);
        Assert.That(plan.Command, Is.Not.WhiteSpace);
        if (Path.IsPathRooted(plan.Command))
            Assert.That(File.Exists(plan.Command), Is.True);
    }

    private static ClaudeCapabilityAdapter Adapter() =>
        new(new ClaudeVersionProvider(new CapabilityCliLocator()));

    private static CapabilityDeclaration Declaration() =>
        new("workspace-agent", "claude", new Dictionary<string, string>(), new Dictionary<string, string>());
}
