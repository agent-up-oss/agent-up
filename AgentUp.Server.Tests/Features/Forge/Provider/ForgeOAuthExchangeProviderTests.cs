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
    public async Task ExchangeAsync_postsToTheModuleTokenUrl()
    {
        var handler = new RecordedHandler(HttpStatusCode.OK, """{"access_token":"exchanged"}""");
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            handler);

        var token = await provider.ExchangeAsync(
            "code",
            "https://example.test/login/oauth/access_token",
            CancellationToken.None);

        Assert.That(token, Is.EqualTo("exchanged"));
        Assert.That(handler.Requests.Single(), Does.Contain("https://example.test/login/oauth/access_token"));
        Assert.That(handler.Requests.Single(), Does.Not.Contain("github.com"));
    }

    [Test]
    public void ExchangeAsync_throwsWhenTheHostOmitsTheToken()
    {
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new RecordedHandler(HttpStatusCode.OK, "{}"));

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExchangeAsync("code", "https://example.test/login/oauth/access_token", CancellationToken.None));

        Assert.That(error!.Message, Is.EqualTo(ForgeHostingCopy.MissingToken));
    }

    [Test]
    public void ExchangeAsync_throwsWhenTheTokenUrlIsMissing()
    {
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new RecordedHandler(HttpStatusCode.OK, """{"access_token":"x"}"""));

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExchangeAsync("code", "", CancellationToken.None));
    }

    [Test]
    public void ExchangeAsync_throwsWhenTheTokenIsEmpty()
    {
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new RecordedHandler(HttpStatusCode.OK, """{"access_token":null}"""));

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExchangeAsync("code", "https://example.test/login/oauth/access_token", CancellationToken.None));

        Assert.That(error!.Message, Is.EqualTo(ForgeHostingCopy.EmptyToken));
    }

    [Test]
    public void ExchangeAsync_throwsWhenTheTokenUrlIsWhitespace()
    {
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new RecordedHandler(HttpStatusCode.OK, """{"access_token":"x"}"""));

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExchangeAsync("code", "  ", CancellationToken.None));

        Assert.That(error!.Message, Is.EqualTo(ForgeHostingCopy.OAuthMissingDetail));
    }

    [Test]
    public void ExchangeAsync_throwsWhenTheAccessTokenIsAnEmptyString()
    {
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new RecordedHandler(HttpStatusCode.OK, """{"access_token":""}"""));

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExchangeAsync("code", "https://example.test/login/oauth/access_token", CancellationToken.None));

        Assert.That(error!.Message, Is.EqualTo(ForgeHostingCopy.EmptyToken));
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
