using System.Net;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentModelProxyForwarderTests
{
    [Test]
    public async Task ForwardAsync_dropsHeadersNominatedByConnection()
    {
        var handler = new RecordingHandler();
        using var forwarder = new AgentModelProxyForwarder(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Connection = "X-Hop-State";
        context.Request.Headers["X-Hop-State"] = "secret";
        context.Request.Headers["X-Model"] = "qwen";

        await forwarder.ForwardAsync(context, new Uri("http://origin.test/v1"), bearerToken: null);

        Assert.Multiple(() =>
        {
            Assert.That(handler.Headers.Keys, Does.Not.Contain("X-Hop-State"));
            Assert.That(handler.Headers["X-Model"], Is.EqualTo(new[] { "qwen" }));
        });
    }

    [Test]
    public async Task ForwardAsync_stripsIncomingAuthorizationWhenReplacingWithNoToken()
    {
        var handler = new RecordingHandler();
        using var forwarder = new AgentModelProxyForwarder(handler);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Authorization = "Bearer sandbox-key";

        await forwarder.ForwardAsync(
            context,
            new Uri("http://origin.test/v1"),
            bearerToken: null,
            replaceIncomingAuthorization: true);

        Assert.That(handler.Headers.Keys, Does.Not.Contain("Authorization"));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Dictionary<string, string[]> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            foreach (var header in request.Headers)
                Headers[header.Key] = header.Value.ToArray();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }
    }
}
