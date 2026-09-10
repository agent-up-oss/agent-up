using System.Text;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Services;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class ApplicationAuditStreamServiceTests
{
    [Test]
    public async Task WriteAsync_WritesMatchingEventsAsServerSentEvents()
    {
        var bus = new AuditEventBus();
        var service = new ApplicationAuditStreamService(bus);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        using var cts = new CancellationTokenSource();

        var writeTask = service.WriteAsync(
            context.Response,
            "ws-1",
            "web",
            ["frontend"],
            null,
            cts.Token);

        bus.Publish(Event("ignored", "health", "ws-1", "web"));
        bus.Publish(Event("live", "frontend", "ws-1", "web"));
        await Task.Delay(50);
        cts.Cancel();

        await writeTask;

        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(context.Response.ContentType, Is.EqualTo("text/event-stream"));
            Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
            Assert.That(body, Does.Contain("data: "));
            Assert.That(body, Does.Contain("\"eventId\":\"live\""));
            Assert.That(body, Does.Not.Contain("\"eventId\":\"ignored\""));
        });
    }

    [Test]
    public async Task WriteAsync_FiltersByStreamSubscription()
    {
        var bus = new AuditEventBus();
        var service = new ApplicationAuditStreamService(bus);
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        using var cts = new CancellationTokenSource();

        var writeTask = service.WriteAsync(
            context.Response,
            "ws-1",
            "web",
            ["application"],
            ["stderr"],
            cts.Token);

        bus.Publish(Event("stdout-line", "application", "ws-1", "web", "stdout"));
        bus.Publish(Event("stderr-line", "application", "ws-1", "web", "stderr"));
        await Task.Delay(50);
        cts.Cancel();

        await writeTask;

        var body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"eventId\":\"stderr-line\""));
            Assert.That(body, Does.Not.Contain("\"eventId\":\"stdout-line\""));
        });
    }

    private static AuditEvent Event(string eventId, string kind, string workspaceId, string application, string? stream = null)
        => new(
            eventId,
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            kind,
            "web",
            "load",
            "success",
            workspaceId,
            null,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, string>
            {
                ["application"] = application,
                ["stream"] = stream ?? string.Empty
            },
            []);

}
