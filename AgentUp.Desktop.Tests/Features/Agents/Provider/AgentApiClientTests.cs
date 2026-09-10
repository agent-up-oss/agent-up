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
    public void ScheduleAsync_surfacesStructuredServerDetail()
    {
        using var handler = new AgentHandler(HttpStatusCode.Conflict, "{\"detail\":\"Agent is already running.\"}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new AgentApiClient(http);

        var exception = Assert.ThrowsAsync<HttpRequestException>(() => client.ScheduleAsync("ws", "Codex", CancellationToken.None));

        Assert.That(exception!.Message, Is.EqualTo("Agent is already running."));
    }
}

internal sealed class AgentHandler(HttpStatusCode status = HttpStatusCode.OK, string? body = null) : HttpMessageHandler
{
    public Uri? Uri { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri = request.RequestUri;
        var text = body ?? "id: 5\nevent: state\ndata: {\"sequence\":5,\"type\":\"state\",\"payload\":{},\"timestamp\":\"2026-01-01T00:00:00Z\"}\n\n";
        var mediaType = body is null ? "text/event-stream" : "application/problem+json";
        var content = new StringContent(text, Encoding.UTF8, mediaType);
        return Task.FromResult(new HttpResponseMessage(status) { Content = content });
    }
}
