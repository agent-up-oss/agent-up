using AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Codex.Tests.Features.CodexCapability.Provider;

[TestFixture]
[CancelAfter(60_000)]
[Timeout(60_000)]
public sealed class CodexInstalledCliSmokeTests
{
    [Test]
    public async Task DiscoverAsync_reportsPresentOrMissingWithoutSkipping()
    {
        var versions = await new CodexVersionProvider(new CapabilityCliLocator())
            .DiscoverAsync(TestContext.CurrentContext.CancellationToken);

        Assert.That(versions, Is.Not.Null);
        foreach (var version in versions)
        {
            Assert.That(version.CapabilityId, Is.EqualTo("codex"));
            Assert.That(version.Version, Is.Not.WhiteSpace);
            Assert.That(version.Location, Is.Not.WhiteSpace);
        }
    }

    [Test]
    public async Task DiscoverAsync_rootedLocationsExist()
    {
        var versions = await new CodexVersionProvider(new CapabilityCliLocator())
            .DiscoverAsync(TestContext.CurrentContext.CancellationToken);

        foreach (var version in versions.Where(version => Path.IsPathRooted(version.Location)))
            Assert.That(File.Exists(version.Location), Is.True);
    }
}
