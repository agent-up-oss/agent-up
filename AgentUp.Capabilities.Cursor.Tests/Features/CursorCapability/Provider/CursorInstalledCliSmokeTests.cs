using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Provider;

[TestFixture]
[CancelAfter(60_000)]
[Timeout(60_000)]
public sealed class CursorInstalledCliSmokeTests
{
    [Test]
    public async Task DiscoverAsync_reportsPresentOrMissingWithoutSkipping()
    {
        var versions = await new CursorVersionProvider(new CapabilityCliLocator())
            .DiscoverAsync(TestContext.CurrentContext.CancellationToken);

        Assert.That(versions, Is.Not.Null);
        foreach (var version in versions)
        {
            Assert.That(version.CapabilityId, Is.EqualTo("cursor"));
            Assert.That(version.Version, Is.Not.WhiteSpace);
            Assert.That(version.Location, Is.Not.WhiteSpace);
        }
    }

    [Test]
    public async Task DiscoverAsync_rootedLocationsExist()
    {
        var versions = await new CursorVersionProvider(new CapabilityCliLocator())
            .DiscoverAsync(TestContext.CurrentContext.CancellationToken);

        foreach (var version in versions.Where(version => Path.IsPathRooted(version.Location)))
            Assert.That(File.Exists(version.Location), Is.True);
    }
}
