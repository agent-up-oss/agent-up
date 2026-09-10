using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class AuditEventMatchingTests
{
    [Test]
    public void MatchesApplication_AcceptsAllSupportedDetailKeys()
    {
        var evt = CreateEvent(new Dictionary<string, string> { ["applicationName"] = "Web" });

        Assert.That(AuditEventMatching.MatchesApplication("web", evt), Is.True);
    }

    [Test]
    public void MatchesStreams_RequiresMatchingStreamForApplicationEvents()
    {
        var stderr = CreateEvent(new Dictionary<string, string>
        {
            ["application"] = "web",
            ["stream"] = "stderr"
        }, kind: "application");

        Assert.Multiple(() =>
        {
            Assert.That(AuditEventMatching.MatchesStreams(["stderr"], stderr), Is.True);
            Assert.That(AuditEventMatching.MatchesStreams(["stdout"], stderr), Is.False);
            Assert.That(AuditEventMatching.MatchesStreams(["stderr"], CreateEvent(new Dictionary<string, string>(), kind: "frontend")), Is.True);
        });
    }

    [Test]
    public void MatchesStreamSubscription_RequiresWorkspaceApplicationKindAndStream()
    {
        var evt = CreateEvent(new Dictionary<string, string>
        {
            ["application"] = "web",
            ["stream"] = "stderr"
        }, kind: "application", workspaceId: "ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(AuditEventMatching.MatchesStreamSubscription("ws-1", "web", ["application"], ["stderr"], evt), Is.True);
            Assert.That(AuditEventMatching.MatchesStreamSubscription("ws-2", "web", ["application"], ["stderr"], evt), Is.False);
            Assert.That(AuditEventMatching.MatchesStreamSubscription("ws-1", "api", ["application"], ["stderr"], evt), Is.False);
            Assert.That(AuditEventMatching.MatchesStreamSubscription("ws-1", "web", ["frontend"], ["stderr"], evt), Is.False);
        });
    }

    private static AuditEvent CreateEvent(
        IReadOnlyDictionary<string, string> details,
        string kind = "frontend",
        string workspaceId = "ws-1")
        => new(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            kind,
            "web",
            "action",
            "success",
            workspaceId,
            null,
            null,
            null,
            null,
            null,
            null,
            details,
            []);

}
