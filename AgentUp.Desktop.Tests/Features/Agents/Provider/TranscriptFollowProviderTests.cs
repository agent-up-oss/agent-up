using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class TranscriptFollowProviderTests
{
    [TestCase(920, 800, 1760, true)]
    [TestCase(960, 800, 1760, true)]
    [TestCase(500, 800, 1760, false)]
    [TestCase(0, 800, 600, true)]
    public void IsFollowing_onlyNearTheEnd(double offset, double viewport, double extent, bool expected)
    {
        Assert.That(TranscriptFollowProvider.IsFollowing(offset, viewport, extent), Is.EqualTo(expected));
    }
}
