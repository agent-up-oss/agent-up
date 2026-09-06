using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Applications.Unit;

[TestFixture]
public sealed class ApplicationMetricsServiceTests
{
    [Test]
    public async Task GetTimelineAsync_BuildsSummaryAndSeries_FromAuditSamples()
    {
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(events: events);
        await audit.RecordAsync(new AuditRecordRequest(
            Kind: "metrics",
            Source: "server",
            Action: "app_metrics_pull",
            Outcome: "success",
            WorkspaceId: "ws-1",
            Details: new Dictionary<string, string>
            {
                ["appName"] = "Web",
                ["metric.latency_ms"] = "120",
                ["metric.requests_per_minute"] = "900",
                ["metric.errors_total"] = "0"
            },
            Scope: AuditScope.Application), CancellationToken.None);
        await audit.RecordAsync(new AuditRecordRequest(
            Kind: "metrics",
            Source: "server",
            Action: "app_metrics_pull",
            Outcome: "success",
            WorkspaceId: "ws-1",
            Details: new Dictionary<string, string>
            {
                ["appName"] = "Web",
                ["metric.latency_ms"] = "142",
                ["metric.requests_per_minute"] = "1200",
                ["metric.errors_total"] = "0"
            },
            Scope: AuditScope.Application), CancellationToken.None);

        var service = new ApplicationMetricsService(audit);
        var timeline = await service.GetTimelineAsync("ws-1", "Web", 60, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(timeline.Summary, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(timeline.Summary.Any(card => card.Label == "Latency"), Is.True);
            Assert.That(timeline.Series.Any(series => series.Key == "latency_ms"), Is.True);
            Assert.That(timeline.Series.First(series => series.Key == "latency_ms").Points, Has.Count.EqualTo(2));
            Assert.That(timeline.Summary.First(card => card.Label == "Req/min").Value, Is.EqualTo("1.2k"));
        });
    }

    [Test]
    public async Task GetTimelineAsync_FormatsZeroValues_WithSensibleDefaults()
    {
        var events = new InMemoryAuditEventRepository();
        var audit = ServerTestComposition.CreateAuditController(events: events);
        await audit.RecordAsync(new AuditRecordRequest(
            Kind: "metrics",
            Source: "server",
            Action: "app_metrics_pull",
            Outcome: "success",
            WorkspaceId: "ws-1",
            Details: new Dictionary<string, string>
            {
                ["appName"] = "Web",
                ["metric.latency_ms"] = "0",
                ["metric.requests_per_minute"] = "0",
                ["metric.errors_total"] = "0"
            },
            Scope: AuditScope.Application), CancellationToken.None);

        var service = new ApplicationMetricsService(audit);
        var timeline = await service.GetTimelineAsync("ws-1", "Web", 60, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(timeline.Summary.First(card => card.Label == "Latency").Value, Is.EqualTo("—"));
            Assert.That(timeline.Summary.First(card => card.Label == "Req/min").Value, Is.EqualTo("0"));
            Assert.That(timeline.Summary.First(card => card.Label == "Errors").Value, Is.EqualTo("0"));
        });
    }
}
