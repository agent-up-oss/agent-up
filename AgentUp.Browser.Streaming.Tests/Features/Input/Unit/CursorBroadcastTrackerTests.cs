namespace AgentUp.Browser.Streaming.Tests.Features.Input.Unit;

[TestFixture]
public sealed class CursorBroadcastTrackerTests
{
    private const string Workspace = "workspace-a";

    [Test]
    public void ShouldBroadcast_isTrueForTheFirstReadingSoViewersLearnTheCursor()
    {
        Assert.That(new CursorBroadcastTracker().ShouldBroadcast(Workspace, "pointer"), Is.True);
    }

    [Test]
    public void ShouldBroadcast_isFalseForAnUnchangedCursor()
    {
        // A pointer move fires continuously; repeating identical frames would flood every
        // connected viewer.
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast(Workspace, "pointer");

        Assert.That(tracker.ShouldBroadcast(Workspace, "pointer"), Is.False);
    }

    [Test]
    public void ShouldBroadcast_isTrueWhenTheCursorChanges()
    {
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast(Workspace, "pointer");

        Assert.That(tracker.ShouldBroadcast(Workspace, "text"), Is.True);
    }

    [Test]
    public void ShouldBroadcast_isTrueAgainWhenTheCursorReturnsToAnEarlierValue()
    {
        // Only the last broadcast value is compared, not the history: going back to
        // "pointer" is a change the viewers have not been told about.
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast(Workspace, "pointer");
        tracker.ShouldBroadcast(Workspace, "text");

        Assert.That(tracker.ShouldBroadcast(Workspace, "pointer"), Is.True);
    }

    [Test]
    public void ShouldBroadcast_tracksEachWorkspaceSeparately()
    {
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast(Workspace, "pointer");

        Assert.That(tracker.ShouldBroadcast("workspace-b", "pointer"), Is.True,
            "One workspace's cursor must not suppress another's first frame.");
    }

    [Test]
    public void ShouldBroadcast_isCaseSensitiveBecauseCssCursorKeywordsAre()
    {
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast(Workspace, "pointer");

        Assert.That(tracker.ShouldBroadcast(Workspace, "POINTER"), Is.True);
    }

    [Test]
    public void Forget_letsTheNextReadingBroadcastAgainAfterASessionEnds()
    {
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast(Workspace, "pointer");

        tracker.Forget(Workspace);

        Assert.That(tracker.ShouldBroadcast(Workspace, "pointer"), Is.True);
    }

    [Test]
    public void Forget_isHarmlessForAWorkspaceThatWasNeverTracked()
    {
        Assert.That(() => new CursorBroadcastTracker().Forget("never-seen"), Throws.Nothing);
    }

    [Test]
    public void Forget_leavesOtherWorkspacesTracked()
    {
        var tracker = new CursorBroadcastTracker();
        tracker.ShouldBroadcast(Workspace, "pointer");
        tracker.ShouldBroadcast("workspace-b", "pointer");

        tracker.Forget("workspace-b");

        Assert.That(tracker.ShouldBroadcast(Workspace, "pointer"), Is.False);
    }
}
