using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Providers;

namespace AgentUp.Desktop.Tests.Features.Git.Provider;

[TestFixture]
public sealed class GitLogLayoutProviderTests
{
    [Test]
    public void Layout_assignsContinuingLanesToFirstParents()
    {
        var parent = new GitLogCommitDto("aaa", "aaa", [], "root", "A", "2026-01-01T00:00:00Z", ["main"]);
        var child = new GitLogCommitDto("bbb", "bbb", ["aaa"], "child", "A", "2026-01-02T00:00:00Z", ["HEAD", "main"]);

        var rows = GitLogLayoutProvider.Layout([child, parent]);

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0].Lane, Is.EqualTo(0));
        Assert.That(rows[1].Lane, Is.EqualTo(0));
        Assert.That(rows[0].CheckoutName, Is.EqualTo("main"));
        Assert.That(rows[0].Outgoing, Has.Exactly(1).Items);
        Assert.That(rows[0].Outgoing[0].ToLane, Is.EqualTo(0));
        Assert.That(rows[1].IncomingLanes, Is.EqualTo(new[] { 0 }));
        Assert.That(rows[0].Refs.Select(item => item.Kind), Is.EqualTo(new[] { "head", "local" }));
    }

    [Test]
    public void Layout_assignsANewLaneToASecondParent()
    {
        var merge = new GitLogCommitDto("ccc", "ccc", ["bbb", "aaa"], "merge", "A", "2026-01-03T00:00:00Z", ["main"]);
        var child = new GitLogCommitDto("bbb", "bbb", ["aaa"], "child", "A", "2026-01-02T00:00:00Z", []);
        var parent = new GitLogCommitDto("aaa", "aaa", [], "root", "A", "2026-01-01T00:00:00Z", []);

        var rows = GitLogLayoutProvider.Layout([merge, child, parent]);

        Assert.That(rows[0].Lane, Is.EqualTo(0));
        Assert.That(rows[0].ParentLanes, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(rows[0].Outgoing.Any(link => link.FromLane == 0 && link.ToLane == 1), Is.True);
        Assert.That(rows[0].LaneCount, Is.EqualTo(2));
        Assert.That(rows[1].IncomingLanes, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(rows[2].Lane, Is.EqualTo(0).Or.EqualTo(1));
    }

    [Test]
    public void FormatTime_usesRelativeMinutes()
    {
        var now = new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero);
        Assert.That(GitLogLayoutProvider.FormatTime("2026-01-03T11:36:00+00:00", now), Is.EqualTo("24 minutes ago"));
    }
}
