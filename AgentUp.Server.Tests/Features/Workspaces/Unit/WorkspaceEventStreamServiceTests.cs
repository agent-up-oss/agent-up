using System.Text;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Providers;
using AgentUp.Server.Features.Workspaces.Services;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceEventStreamServiceTests
{
    [Test]
    public async Task WriteAsync_WritesHeadersAndPublishedEvents()
    {
        var bus = new WorkspaceEventBus();
        var service = new WorkspaceEventStreamService(bus, new WorkspaceEventFrameProvider());
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var writeTask = service.WriteAsync(context.Response, cts.Token);
        bus.Publish(new WorkspaceStateChangedEvent("ws-1", "Running", [new AppStateChange("Web", "Running")]));

        await WaitForAsync(() => body.Length > 0, cts.Token);
        await cts.CancelAsync();
        await writeTask;

        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Multiple(() =>
        {
            Assert.That(context.Response.ContentType, Is.EqualTo("text/event-stream"));
            Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-cache"));
            Assert.That(context.Response.Headers.Connection.ToString(), Is.EqualTo("keep-alive"));
            Assert.That(text, Does.StartWith("data: "));
            Assert.That(text, Does.Contain("\"workspaceId\":\"ws-1\""));
        });
    }

    private static async Task WaitForAsync(Func<bool> predicate, CancellationToken ct)
    {
        while (!predicate())
            await Task.Delay(10, ct);
    }
}
