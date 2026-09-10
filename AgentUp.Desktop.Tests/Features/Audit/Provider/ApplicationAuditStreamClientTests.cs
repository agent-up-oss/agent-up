using System.Net;
using System.Text;
using System.Text.Json;
using AgentUp.Desktop.Features.Audit.DTOs;
using AgentUp.Desktop.Features.Audit.Providers;

namespace AgentUp.Desktop.Tests.Features.Audit.Provider;

[TestFixture]
public sealed class ApplicationAuditStreamClientTests
{
    [Test]
    public async Task StreamAsync_DeserializesServerSentEventLines()
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventId = "live-1",
            timestamp = "2026-08-22T12:00:00Z",
            kind = "frontend",
            action = "load_failed",
            outcome = "failure",
            details = new Dictionary<string, string> { ["application"] = "web", ["message"] = "Load failed" }
        });
        var body = $"data: {payload}\n\n";
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new ApplicationAuditStreamClient(http);
        ApplicationAuditEventDto? received = null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await client.StreamAsync("ws-1", "web", ["frontend"], [], evt => received = evt, cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(received, Is.Not.Null);
            Assert.That(received!.EventId, Is.EqualTo("live-1"));
            Assert.That(received.Details["message"], Is.EqualTo("Load failed"));
        });
    }

    [Test]
    public async Task StreamAsync_SkipsMalformedEventLines()
    {
        var body = "data: not-json\n\n";
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream")
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new ApplicationAuditStreamClient(http);
        var count = 0;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await client.StreamAsync("ws-1", "web", ["frontend"], [], _ => count++, cts.Token);

        Assert.That(count, Is.Zero);
    }

    [Test]
    public async Task StreamAsync_IncludesKindAndStreamQueryParameters()
    {
        Uri? requested = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            requested = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "text/event-stream")
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new ApplicationAuditStreamClient(http);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await client.StreamAsync("ws-1", "web", ["frontend", "health"], ["stderr"], _ => { }, cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(requested, Is.Not.Null);
            Assert.That(requested!.AbsolutePath, Does.EndWith("/applications/web/stream"));
            Assert.That(requested.Query, Does.Contain("kinds=frontend"));
            Assert.That(requested.Query, Does.Contain("kinds=health"));
            Assert.That(requested.Query, Does.Contain("streams=stderr"));
        });
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
