namespace AgentUp.Browser.Streaming.Tests.Features.Input.Unit;

[TestFixture]
public sealed class CursorBroadcastTrackerTests
{
    [Test]
    public void ShouldBroadcast_isTrueForTheFirstReadingSoViewersLearnTheCursor()
    {
        Assert.That(new CursorBroadcastTracker().ShouldBroadcast("pointer"), Is.True);
    }

    [Test]
    public void ShouldBroadcast_isFalseForAnUnchangedCursor()
    {
        // A pointer move fires continuously; repeating identical frames would flood every
        // connected viewer.
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast("pointer");

        Assert.That(tracker.ShouldBroadcast("pointer"), Is.False);
    }

    [Test]
    public void ShouldBroadcast_isTrueWhenTheCursorChanges()
    {
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast("pointer");

        Assert.That(tracker.ShouldBroadcast("text"), Is.True);
    }

    [Test]
    public void ShouldBroadcast_isTrueAgainWhenTheCursorReturnsToAnEarlierValue()
    {
        // Only the last broadcast value is compared, not the history: going back to
        // "pointer" is a change the viewers have not been told about.
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast("pointer");
        tracker.ShouldBroadcast("text");

        Assert.That(tracker.ShouldBroadcast("pointer"), Is.True);
    }

    [Test]
    public void ShouldBroadcast_isCaseSensitiveBecauseCssCursorKeywordsAre()
    {
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast("pointer");

        Assert.That(tracker.ShouldBroadcast("POINTER"), Is.True);
    }

    [Test]
    public void ShouldBroadcast_startsFreshForASeparateSession()
    {
        // Each session owns its own tracker, so a new session reports its first cursor even
        // when the previous session on the same workspace ended on that same value. As a
        // singleton keyed by workspace this frame was suppressed.
        var ended = new CursorBroadcastTracker();
        ended.ShouldBroadcast("pointer");

        Assert.That(new CursorBroadcastTracker().ShouldBroadcast("pointer"), Is.True);
    }
}
