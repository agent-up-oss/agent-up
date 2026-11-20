using System.Net;
using System.Text;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Tests.Fake;
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
    public async Task ExchangeAsync_sendsTheAuthorizationCodeGrantAsAForm()
    {
        var handler = new RecordedHandler(HttpStatusCode.OK, """{"access_token":"exchanged"}""");
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            handler);

        await provider.ExchangeAsync("the-code", "https://example.test/login/oauth/access_token", CancellationToken.None);

        // Forgejo answers 400 to a token request that does not name its grant.
        Assert.That(handler.Bodies.Single(), Does.StartWith("application/x-www-form-urlencoded ")
            .And.Contain("grant_type=authorization_code")
            .And.Contain("code=the-code"));
    }

    [Test]
    public async Task ExchangeGrantAsync_keepsTheRefreshTokenAndWhenTheAccessTokenExpires()
    {
        var clock = new StubTimeProvider();
        var handler = new RecordedHandler(HttpStatusCode.OK, """{"access_token":"a","refresh_token":"r","expires_in":3600}""");
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            handler,
            clock);

        var grant = await provider.ExchangeGrantAsync("code", "https://example.test/login/oauth/access_token", CancellationToken.None);

        Assert.That(grant, Is.EqualTo(new ForgeOAuthGrant("a", "r", clock.UtcNow.AddHours(1))));
    }

    [Test]
    public async Task RefreshAsync_sendsTheRefreshTokenGrant()
    {
        var handler = new RecordedHandler(HttpStatusCode.OK, """{"access_token":"b","refresh_token":"r2","expires_in":3600}""");
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            handler);

        var grant = await provider.RefreshAsync("r1", "https://example.test/login/oauth/access_token", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(grant.AccessToken, Is.EqualTo("b"));
            Assert.That(handler.Bodies.Single(), Does.Contain("grant_type=refresh_token").And.Contain("refresh_token=r1"));
        });
    }

    [Test]
    public void ExchangeAsync_reportsTheHostsRefusal()
    {
        var handler = new RecordedHandler(HttpStatusCode.BadRequest, """{"error":"unsupported_grant_type"}""");
        var provider = new ForgeOAuthExchangeProvider(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            handler);

        var error = Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.ExchangeAsync("code", "https://example.test/login/oauth/access_token", CancellationToken.None));

        Assert.That(error!.Message, Does.Contain("400").And.Contain("unsupported_grant_type"));
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

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri}");
            Bodies.Add(request.Content is null ? "" : $"{request.Content.Headers.ContentType?.MediaType} {await request.Content.ReadAsStringAsync(cancellationToken)}");
            return Respond();
        }

        private HttpResponseMessage Respond()
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
