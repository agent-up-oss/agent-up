using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Shared.Providers;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.IdentityProvider.Provider;

// The path a person actually walks: no pre-approval, so the consent page is shown, approved by
// hand, and the redirect carries the code back. The suites pre-approve to stay deterministic,
// which means this - the only path a real user takes - is otherwise never exercised.
// Real HTTP, so these take longer than the 30-second default in coverlet.runsettings.
[TestFixture, CancelAfter(120_000)]
public sealed class TestIdentityProviderConsentTests
{
    private TestIdentityProviderService _provider = null!;
    private HttpClient _client = null!;
    private string _origin = null!;

    [OneTimeSetUp]
    public void StartProvider()
    {
        _provider = new TestIdentityProviderService(0, null);
        _provider.Start();
        _origin = $"http://localhost:{_provider.Port}";
        // The redirect is the thing under test, so it is read rather than followed.
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    [OneTimeTearDown]
    public async Task StopProvider()
    {
        _client.Dispose();
        await _provider.DisposeAsync();
    }

    [Test]
    public async Task ApprovingByHand_carriesTheCodeBackAndExchangesForAToken()
    {
        var verifier = PkceVerifier.Secret();
        var challenge = PkceVerifier.Challenge(verifier);
        const string redirectUri = "http://127.0.0.1:1/auth/callback";

        using var consent = await _client.GetAsync(
            $"{_origin}/oauth/authorize?response_type=code&client_id=walks-in" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}&state=st" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}&code_challenge_method=S256");
        var page = await consent.Content.ReadAsStringAsync();

        // Everything the approval needs has to survive on the page, or the sign-in cannot finish.
        Assert.Multiple(() =>
        {
            Assert.That(consent.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(page, Does.Contain("id=\"approve\""));
            Assert.That(page, Does.Contain(challenge), "The challenge has to reach the approval");
        });

        using var approved = await FormPost.SendAsync(
            _client,
            $"{_origin}/oauth/approve",
            ("client_id", "walks-in"),
            ("redirect_uri", redirectUri),
            ("state", "st"),
            ("code_challenge", challenge));

        Assert.That(approved.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        var location = approved.Headers.Location!.ToString();
        var code = FormReader.Query(location[location.IndexOf('?')..]).GetValueOrDefault("code");

        using var token = await FormPost.SendAsync(
            _client,
            $"{_origin}/oauth/token",
            ("grant_type", "authorization_code"),
            ("code", code!),
            ("code_verifier", verifier));
        var payload = await token.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(location, Does.StartWith(redirectUri));
            Assert.That(location, Does.Contain("state=st"));
            Assert.That(token.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(payload.GetProperty("access_token").GetString(), Does.StartWith("test-oat-"));
            Assert.That(payload.GetProperty("token_type").GetString(), Is.EqualTo("Bearer"));
        });
    }

    // A redirect that already carries a query has to keep it, or the agent's own listener stops
    // recognising the address it advertised.
    [Test]
    public async Task ApprovingByHand_appendsToARedirectThatAlreadyHasAQuery()
    {
        using var approved = await FormPost.SendAsync(
            _client,
            $"{_origin}/oauth/approve",
            ("client_id", "already-has-a-query"),
            ("redirect_uri", "http://127.0.0.1:1/auth/callback?flow=test"));

        var location = approved.Headers.Location!.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(location, Does.Contain("flow=test"));
            Assert.That(location, Does.Contain("&code="), "A second parameter is appended, not started");
        });
    }

    // Agents that do not name themselves still have to be able to sign in, because the shape the
    // provider stands in for does not always send a client id.
    [Test]
    public async Task AnUnnamedClient_isStillGivenAGrant()
    {
        using var device = await FormPost.SendAsync(_client, $"{_origin}/oauth/device/code");
        var deviceGrant = await device.Content.ReadFromJsonAsync<JsonElement>();

        using var login = await FormPost.SendAsync(_client, $"{_origin}/login/start");
        var loginGrant = await login.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(deviceGrant.GetProperty("user_code").GetString(), Is.Not.Empty);
            Assert.That(deviceGrant.GetProperty("verification_uri").GetString(), Does.EndWith("/device"));
            Assert.That(deviceGrant.GetProperty("interval").GetInt32(), Is.GreaterThan(0));
            Assert.That(loginGrant.GetProperty("login_id").GetString(), Is.Not.Empty);
            Assert.That(loginGrant.GetProperty("status_url").GetString(), Does.Contain("/status"));
        });
    }
}
