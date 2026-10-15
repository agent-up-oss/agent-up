using System.Net;
using System.Text;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Forge.Provider;

[TestFixture]
public sealed class ForgeOAuthExchangeProviderTests
{
    [Test]
    public async Task ExchangeAsync_readsTheAccessTokenFromJson()
    {
        var handler = new RecordedHandler(HttpStatusCode.OK, """{"access_token":"ghp_exchanged"}""");
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            handler);

        var token = await provider.ExchangeAsync("code", CancellationToken.None);

        Assert.That(token, Is.EqualTo("ghp_exchanged"));
        Assert.That(handler.Requests.Single(), Does.Contain("https://github.com/login/oauth/access_token"));
    }

    [Test]
    public void ExchangeAsync_throwsWhenTheHostOmitsTheToken()
    {
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new RecordedHandler(HttpStatusCode.OK, "{}"));

        Assert.ThrowsAsync<InvalidOperationException>(() => provider.ExchangeAsync("code", CancellationToken.None));
    }

    private sealed class RecordedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri}");
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
