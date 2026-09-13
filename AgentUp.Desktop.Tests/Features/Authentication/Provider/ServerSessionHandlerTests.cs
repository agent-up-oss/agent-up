using System.Net;
using System.Net.Http.Headers;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class ServerSessionHandlerTests
{
    [Test]
    public void SendAsync_throwsWhenNoServerUrlHasBeenApplied()
    {
        using var http = new HttpClient(new ServerSessionHandler(new RecordingHandler()))
        {
            BaseAddress = new Uri("http://127.0.0.1:5000/")
        };

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await http.GetAsync("/api/auth/status"));
        Assert.That(exception!.Message, Is.EqualTo("The Desktop HTTP session has no Server URL."));
    }

    [Test]
    public async Task SendAsync_rewritesAbsoluteUrlsOntoTheSessionOrigin()
    {
        var inner = new RecordingHandler();
        var session = new ServerSessionHandler(inner);
        session.Apply(new Uri("http://127.0.0.1:5100/"), "token-1");
        using var http = new HttpClient(session)
        {
            BaseAddress = new Uri("http://127.0.0.1:5000/")
        };

        _ = await http.GetAsync("http://127.0.0.1:5000/api/auth/status?q=1#frag");

        Assert.Multiple(() =>
        {
            Assert.That(inner.LastUri, Is.EqualTo(new Uri("http://127.0.0.1:5100/api/auth/status?q=1#frag")));
            Assert.That(inner.LastAuthorization?.Parameter, Is.EqualTo("token-1"));
        });
    }

    [Test]
    public async Task SendAsync_clearsAStaleAuthorizationHeaderWhenTheSessionHasNoToken()
    {
        var inner = new RecordingHandler();
        var session = new ServerSessionHandler(inner);
        session.Apply(new Uri("http://127.0.0.1:5100/"), "  ");
        using var http = new HttpClient(session)
        {
            BaseAddress = new Uri("http://127.0.0.1:5100/")
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "old");

        _ = await http.SendAsync(request);

        Assert.That(inner.LastAuthorization, Is.Null);
        Assert.That(inner.LastUri, Is.EqualTo(new Uri("http://127.0.0.1:5100/api/auth/status")));
    }

    [Test]
    public async Task SendAsync_usesTheSessionOriginForRelativeAndMissingUris()
    {
        var inner = new RecordingHandler();
        var session = new ServerSessionHandler(inner);
        session.Apply(new Uri("http://127.0.0.1:5100/"), null);
        using var invoker = new HttpMessageInvoker(session);

        _ = await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, (Uri?)null), CancellationToken.None);
        Assert.That(inner.LastUri, Is.EqualTo(new Uri("http://127.0.0.1:5100/")));

        _ = await invoker.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, new Uri("/api/auth/status", UriKind.Relative)),
            CancellationToken.None);
        Assert.That(inner.LastUri, Is.EqualTo(new Uri("http://127.0.0.1:5100/api/auth/status")));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastAuthorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
