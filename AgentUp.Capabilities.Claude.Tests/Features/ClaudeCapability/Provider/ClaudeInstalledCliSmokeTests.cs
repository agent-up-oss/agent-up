using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Provider;

[TestFixture]
[CancelAfter(60_000)]
[Timeout(60_000)]
public sealed class ClaudeInstalledCliSmokeTests
{
    [Test]
    public async Task DiscoverAsync_reportsPresentOrMissingWithoutSkipping()
    {
        var versions = await new ClaudeVersionProvider(new CapabilityCliLocator())
            .DiscoverAsync(TestContext.CurrentContext.CancellationToken);

        Assert.That(versions, Is.Not.Null);
        foreach (var version in versions)
        {
            Assert.That(version.CapabilityId, Is.EqualTo("claude"));
            Assert.That(version.Version, Is.Not.WhiteSpace);
            Assert.That(version.Location, Is.Not.WhiteSpace);
        }
    }

    [Test]
    public async Task DiscoverAsync_rootedLocationsExist()
    {
        var versions = await new ClaudeVersionProvider(new CapabilityCliLocator())
            .DiscoverAsync(TestContext.CurrentContext.CancellationToken);

        foreach (var version in versions.Where(version => Path.IsPathRooted(version.Location)))
            Assert.That(File.Exists(version.Location), Is.True);
    }
}
