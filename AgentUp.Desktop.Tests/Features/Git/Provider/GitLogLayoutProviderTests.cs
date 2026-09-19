using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Git.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Git.Provider;

[TestFixture]
public sealed class GitLogLayoutProviderTests
{
    [Test]
    public void Layout_assignsContinuingLanesToFirstParents()
    {
        var parent = Commit("aaa", [], "root", "2026-01-01T00:00:00Z", "main");
        var child = Commit("bbb", ["aaa"], "child", "2026-01-02T00:00:00Z", "HEAD", "main");

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
        var merge = Commit("ccc", ["bbb", "aaa"], "merge", "2026-01-03T00:00:00Z", "main");
        var child = Commit("bbb", ["aaa"], "child", "2026-01-02T00:00:00Z");
        var parent = Commit("aaa", [], "root", "2026-01-01T00:00:00Z");

        var rows = GitLogLayoutProvider.Layout([merge, child, parent]);

        Assert.That(rows[0].Lane, Is.EqualTo(0));
        Assert.That(rows[0].ParentLanes, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(rows[0].Outgoing.Any(link => link.FromLane == 0 && link.ToLane == 1), Is.True);
        Assert.That(rows[0].LaneCount, Is.EqualTo(2));
        Assert.That(rows[1].IncomingLanes, Is.EqualTo(new[] { 0, 1 }));
        Assert.That(rows[2].Lane, Is.EqualTo(0).Or.EqualTo(1));
    }

    [Test]
    public void Layout_reusesAFreedLaneForASecondParent()
    {
        var c1 = Commit("c1", ["m"], "child-m", "2026-01-07T00:00:00Z");
        var c2 = Commit("c2", ["s"], "child-s", "2026-01-06T00:00:00Z");
        var c3 = Commit("c3", ["a"], "child-a", "2026-01-05T00:00:00Z");
        var s = Commit("s", [], "side", "2026-01-04T00:00:00Z");
        var m = Commit("m", ["a", "b"], "merge", "2026-01-03T00:00:00Z", "origin/topic");
        var a = Commit("a", [], "left", "2026-01-02T00:00:00Z");
        var b = Commit("b", [], "right", "2026-01-01T00:00:00Z");

        var rows = GitLogLayoutProvider.Layout([c1, c2, c3, s, m, a, b]);

        Assert.That(rows[4].Outgoing.Any(link => link.FromLane != link.ToLane), Is.True);
        Assert.That(rows[4].CheckoutName, Is.EqualTo("origin/topic"));
        Assert.That(GitLogLayoutProvider.ClassifyRef("origin/topic").Kind, Is.EqualTo("remote"));
        Assert.That(GitLogLayoutProvider.LaneX(1), Is.EqualTo(GitLogLayoutProvider.LaneWidth + GitLogLayoutProvider.LaneWidth / 2));
        Assert.That(GitLogLayoutProvider.GraphWidth(0), Is.EqualTo(GitLogLayoutProvider.LaneWidth));
    }

    [Test]
    public void FormatTime_usesRelativeMinutes()
    {
        var now = new DateTimeOffset(2026, 1, 3, 12, 0, 0, TimeSpan.Zero);
        Assert.That(GitLogLayoutProvider.FormatTime("2026-01-03T11:36:00+00:00", now), Is.EqualTo("24 minutes ago"));
        Assert.That(GitLogLayoutProvider.FormatTime("2026-01-03T12:00:00+00:00", now), Is.EqualTo("just now"));
        Assert.That(GitLogLayoutProvider.FormatTime("2026-01-03T11:59:00+00:00", now), Is.EqualTo("1 minute ago"));
        Assert.That(GitLogLayoutProvider.FormatTime("2026-01-03T08:05:00+00:00", now), Does.StartWith("Today "));
        Assert.That(GitLogLayoutProvider.FormatTime("2026-01-02T21:05:00+00:00", now), Does.StartWith("Yesterday "));
        Assert.That(GitLogLayoutProvider.FormatTime("2025-12-01T09:00:00+00:00", now), Does.Contain("01.12.25"));
        Assert.That(GitLogLayoutProvider.FormatTime("not-a-date", now), Is.EqualTo("not-a-date"));
    }

    [Test]
    public void GitLogRefDto_exposesCheckoutKinds()
    {
        Assert.That(new GitLogRefDto("main", "local").CanCheckout, Is.True);
        Assert.That(new GitLogRefDto("HEAD", "head").IsHead, Is.True);
        Assert.That(new GitLogRefDto("origin/main", "remote").IsRemote, Is.True);
        Assert.That(new GitHeadStateDto("main", ["main"]).Ahead, Is.EqualTo(0));
    }

    private static GitLogCommitDto Commit(string id, string[] parents, string subject, string timestamp, params string[] refs)
        => DesktopDomain.GitCommit()
            .WithId(id)
            .WithParents(parents)
            .Saying(subject)
            .At(timestamp)
            .WithRefs(refs)
            .Build();
}
