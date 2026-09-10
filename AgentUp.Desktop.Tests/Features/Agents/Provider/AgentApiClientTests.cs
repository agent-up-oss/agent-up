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
}

internal sealed class AgentHandler : HttpMessageHandler
{
    public Uri? Uri { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri = request.RequestUri;
        var content = new StringContent("id: 5\nevent: state\ndata: {\"sequence\":5,\"type\":\"state\",\"payload\":{},\"timestamp\":\"2026-01-01T00:00:00Z\"}\n\n", Encoding.UTF8, "text/event-stream");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
