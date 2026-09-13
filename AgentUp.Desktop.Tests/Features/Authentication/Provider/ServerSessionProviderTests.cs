using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AgentUp.Desktop.Features.Authentication.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class ServerSessionProviderTests
{
    [Test]
    public void Apply_setsBaseAddressAndBearerToken()
    {
        using var http = new HttpClient();

        ServerSessionProvider.Apply(http, new Uri("http://127.0.0.1:5100/"), "token-1");

        Assert.Multiple(() =>
        {
            Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
            Assert.That(http.DefaultRequestHeaders.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Bearer", "token-1")));
        });
    }

    [Test]
    public void Apply_clearsAuthorizationWhenTokenIsMissing()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "old");

        ServerSessionProvider.Apply(http, new Uri("https://agent-up.example.com"), null);

        Assert.That(http.DefaultRequestHeaders.Authorization, Is.Null);
    }

    [Test]
    public async Task Apply_afterARequest_sendsTheNextCallToTheNewServer()
    {
        Uri? lastUri = null;
        using var http = new DisposableTestHttpClient(request =>
        {
            lastUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        });

        _ = await http.Client.GetAsync("/api/auth/status");
        Assert.That(lastUri!.Port, Is.EqualTo(5000));

        ServerSessionProvider.Apply(http.Client, new Uri("http://127.0.0.1:5100/"), "token-1");
        _ = await http.Client.GetAsync("/api/auth/status");

        Assert.Multiple(() =>
        {
            Assert.That(lastUri!.Port, Is.EqualTo(5100));
            Assert.That(http.Client.DefaultRequestHeaders.Authorization?.Parameter, Is.EqualTo("token-1"));
        });
    }

    [Test]
    public void CreateClient_tracksTheSessionForLaterApply()
    {
        using var http = ServerSessionProvider.CreateClient(new Uri("http://127.0.0.1:5000/"), new RecordingHandler());
        http.BaseAddress = null;

        ServerSessionProvider.Apply(http, new Uri("http://127.0.0.1:5100/"), " ");

        Assert.Multiple(() =>
        {
            Assert.That(http.BaseAddress, Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
            Assert.That(http.DefaultRequestHeaders.Authorization, Is.Null);
            Assert.That(ServerSessionProvider.CurrentUri(http), Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
        });
    }

    [Test]
    public void CurrentUri_fallsBackToBaseAddressWhenTheClientHasNoSession()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5100/") };

        Assert.That(ServerSessionProvider.CurrentUri(http), Is.EqualTo(new Uri("http://127.0.0.1:5100/")));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
