using AgentUp.Desktop.Shared.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class MotionProviderTests
{
    [Test]
    public void FollowStep_easesTowardTheEndWithoutOvershooting()
    {
        var first = MotionProvider.FollowStep(0, 1000, 16);
        var second = MotionProvider.FollowStep(first, 1000, 16);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.InRange(100, 300), "a frame moves a good part of the way");
            Assert.That(second - first, Is.LessThan(first), "and each frame less than the one before");
            Assert.That(MotionProvider.FollowStep(999.7, 1000, 16), Is.EqualTo(1000), "the last half pixel snaps");
        });
    }

    [Test]
    public void FollowStep_movesFurtherAfterASlowFrame()
    {
        Assert.That(MotionProvider.FollowStep(0, 1000, 64), Is.GreaterThan(MotionProvider.FollowStep(0, 1000, 16)));
    }

    [Test]
    public void RevealStep_catchesUpWithABurstWithinAFewFrames()
    {
        var shown = 0;
        var frames = 0;
        while (shown < 600 && frames < 100)
        {
            shown = MotionProvider.RevealStep(shown, 600, 16);
            frames++;
        }

        // A 600-character chunk flows in over roughly a quarter of a second rather than at once.
        Assert.That(frames, Is.InRange(5, 20));
    }

    [Test]
    public void RevealStep_neverPassesTheTextThatArrived()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MotionProvider.RevealStep(10, 11, 1000), Is.EqualTo(11));
            Assert.That(MotionProvider.RevealStep(12, 11, 16), Is.EqualTo(11));
        });
    }
}
