using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Providers;

namespace AgentUp.Server.Tests.Features.Audit.Provider;

[TestFixture]
public sealed class AuditEventLogLinePrefilterTests
{
    [Test]
    public void MightMatch_RejectsLinesMissingWorkspaceApplicationKindOrStream()
    {
        const string line = """{"EventId":"evt-1","WorkspaceId":"ws-1","Kind":"application","Details":{"application":"web","stream":"stderr"}}""";
        var query = new AuditEventQuery(
            "ws-1",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            1,
            Application: "web",
            Kinds: ["application"],
            Streams: ["stderr"]);

        Assert.Multiple(() =>
        {
            Assert.That(AuditEventLogLinePrefilter.MightMatch(query, line), Is.True);
            Assert.That(AuditEventLogLinePrefilter.MightMatch(query with { WorkspaceId = "ws-2" }, line), Is.False);
            Assert.That(AuditEventLogLinePrefilter.MightMatch(query with { Application = "api" }, line), Is.False);
            Assert.That(AuditEventLogLinePrefilter.MightMatch(query with { Kinds = ["frontend"] }, line), Is.False);
            Assert.That(AuditEventLogLinePrefilter.MightMatch(query with { Streams = ["stdout"] }, line), Is.False);
            Assert.That(AuditEventLogLinePrefilter.MightMatch(query, string.Empty), Is.False);
        });
    }
}
