using System.Net;
using System.Text;
using AgentUp.Desktop.Features.Agents.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentApiClientTests
{
    [Test]
    public async Task EventsAsync_parsesSseDataAndEscapesWorkspaceId()
    {
        using var handler = new AgentHandler(); using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);
        await foreach (var item in client.EventsAsync("ws 1", 4, CancellationToken.None)) Assert.That(item.Sequence, Is.EqualTo(5));
        Assert.That(handler.Uri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws%201/agent/events?after=4"));
    }

    [Test]
    public async Task EventsAsync_skipsMalformedDataFramesAndContinues()
    {
        const string sse = "data: not-json\n\ndata: {\"sequence\":5,\"type\":\"state\",\"payload\":{},\"timestamp\":\"2026-01-01T00:00:00Z\"}\n\n";
        using var handler = new AgentHandler(HttpStatusCode.OK, sse, eventStream: true);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);
        var items = new List<long>();

        await foreach (var item in client.EventsAsync("ws", 0, CancellationToken.None))
            items.Add(item.Sequence);

        Assert.That(items, Is.EqualTo(new[] { 5L }));
    }

    [Test]
    public void ScheduleAsync_surfacesStructuredServerDetail()
    {
        using var handler = new AgentHandler(HttpStatusCode.Conflict, "{\"detail\":\"Agent is already running.\"}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);

        var exception = Assert.ThrowsAsync<HttpRequestException>(() => client.ScheduleAsync("ws", "Codex", CancellationToken.None));

        Assert.That(exception!.Message, Is.EqualTo("Agent is already running."));
    }

    [Test]
    public async Task AuthenticateDecideAndStop_callWorkspaceScopedRoutes()
    {
        using var handler = new AgentHandler(HttpStatusCode.Accepted, "{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);

        await client.AuthenticateAsync("ws", "chatgpt", CancellationToken.None);
        Assert.That(handler.Uri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws/agent/authenticate"));
        await client.DecideAsync("ws", "req", "allow", CancellationToken.None);
        Assert.That(handler.Uri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws/agent/permissions"));
        await client.StopAsync("ws", CancellationToken.None);
        Assert.That(handler.Uri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws/agent"));
    }

    [Test]
    public async Task SendAsync_postsTheWorkspaceMessage()
    {
        using var handler = new AgentHandler(HttpStatusCode.Accepted, "{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);

        await client.SendAsync("ws", "hello", CancellationToken.None);

        Assert.That(handler.Uri!.PathAndQuery, Is.EqualTo("/api/workspaces/ws/agent/messages"));
    }

    [Test]
    public void ScheduleAsync_ignoresEmptyProblemBodies()
    {
        using var handler = new AgentHandler(HttpStatusCode.BadGateway, "");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);

        var exception = Assert.ThrowsAsync<HttpRequestException>(() => client.ScheduleAsync("ws", "Codex", CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("HTTP 502"));
    }

    [Test]
    public void ScheduleAsync_ignoresMalformedProblemBodies()
    {
        using var handler = new AgentHandler(HttpStatusCode.InternalServerError, "not-json");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);

        var exception = Assert.ThrowsAsync<HttpRequestException>(() => client.ScheduleAsync("ws", "Codex", CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("HTTP 500"));
    }
}

internal sealed class AgentHandler(HttpStatusCode status = HttpStatusCode.OK, string? body = null, bool eventStream = false) : HttpMessageHandler
{
    public Uri? Uri { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri = request.RequestUri;
        var text = body ?? "id: 5\nevent: state\ndata: {\"sequence\":5,\"type\":\"state\",\"payload\":{},\"timestamp\":\"2026-01-01T00:00:00Z\"}\n\n";
        var mediaType = eventStream || body is null ? "text/event-stream" : "application/problem+json";
        var content = new StringContent(text, Encoding.UTF8, mediaType);
        return Task.FromResult(new HttpResponseMessage(status) { Content = content });
    }
}
