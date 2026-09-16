using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.TestAgents.Features.Authentication.Providers;
using AgentUp.TestAgents.Shared.Providers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.IdentityProvider.Provider;

// Real HTTP against the real provider, driving each login flow the way its agent does. Nothing
// here is stubbed: if PKCE, the device grant, or the poll contract is wrong, these fail.
// Real network and real processes, so these take longer than the 30-second default in
// coverlet.runsettings.
[TestFixture, CancelAfter(120_000)]
public sealed class TestIdentityProviderServiceTests
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
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    [OneTimeTearDown]
    public async Task StopProvider()
    {
        _client.Dispose();
        await _provider.DisposeAsync();
    }

    [SetUp]
    public async Task Reset()
    {
        using var response = await FormPost.SendAsync(_client, $"{_origin}/test/reset");
        Assert.That(response.IsSuccessStatusCode, Is.True, "The provider must be reachable before each test");
    }

    [Test]
    public async Task DeviceCodeFlow_signsInOnceTheUserCodeIsApproved()
    {
        var flow = new DeviceCodeLoginFlow(_client, _origin);
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var login = flow.RunAsync(output, TextReader.Null, cancellation.Token);

        // Approve through the control plane rather than waiting on a poll interval.
        var userCode = await WaitForUserCodeAsync(output, cancellation.Token);
        using var approved = await FormPost.SendAsync(
            _client,
            $"{_origin}/test/approve",
            cancellation.Token,
            ("user_code", userCode));

        Assert.That(approved.IsSuccessStatusCode, Is.True);
        var token = await login;

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Not.Null.And.StartWith("test-oat-"));
            Assert.That(output.ToString(), Does.Contain("/device"), "The link must be printed for the client to open");
            Assert.That(output.ToString(), Does.Contain(userCode), "The user code must be printed for the client to show");
        });
    }

    [Test]
    public async Task LoopbackRedirectFlow_signsInThroughTheAgentsOwnListener()
    {
        // Pre-approving means the authorization request redirects straight back, so the agent's
        // real callback listener runs without a browser having to click anything.
        using var preApproved = await FormPost.SendAsync(
            _client,
            $"{_origin}/test/pre-approve",
            ("client_id", "test-agent1"));
        Assert.That(preApproved.IsSuccessStatusCode, Is.True);

        var flow = new LoopbackRedirectLoginFlow(_client, _origin);
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var login = flow.RunAsync(output, TextReader.Null, cancellation.Token);
        var authorizeUrl = await output.WaitForLineContainingAsync("/oauth/authorize", cancellation.Token);

        // Stand in for the browser: follow the link, which redirects onto the agent's listener.
        using var browser = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var page = await browser.GetAsync(authorizeUrl, cancellation.Token);
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var token = await login;
        Assert.That(token, Is.Not.Null.And.StartWith("test-oat-"));
    }

    [Test]
    public async Task PastedCodeFlow_signsInWhenTheCodeIsPastedBack()
    {
        using var preApproved = await FormPost.SendAsync(
            _client,
            $"{_origin}/test/pre-approve",
            ("client_id", "test-agent3"));
        Assert.That(preApproved.IsSuccessStatusCode, Is.True);

        var flow = new PastedCodeLoginFlow(_client, _origin);
        using var output = new AgentOutput();
        using var input = new PipedReader();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var login = flow.RunAsync(output, input, cancellation.Token);

        var authorizeUrl = await output.WaitForLineContainingAsync("/oauth/authorize", cancellation.Token);
        using var browser = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var page = await browser.GetAsync(authorizeUrl, cancellation.Token);
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Read the code out of the provider the way a user reads it off the page.
        using var latest = await _client.GetAsync($"{_origin}/test/latest-code?client_id=test-agent3", cancellation.Token);
        var code = (await latest.Content.ReadFromJsonAsync<JsonElement>(cancellation.Token)).GetProperty("code").GetString();
        input.Write(code!);

        var token = await login;

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Not.Null.And.StartWith("sk-ant-oat01-"));
            Assert.That(output.ToString(), Does.Contain("Paste code here: "),
                "The prompt shape is the point of this agent and must not drift");
        });
    }

    [Test]
    public async Task SilentPollFlow_signsInOnceTheLoginIsApprovedAndShowsNoUserCode()
    {
        var flow = new SilentPollLoginFlow(_client, _origin);
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var login = flow.RunAsync(output, TextReader.Null, cancellation.Token);

        var loginUrl = await output.WaitForLineContainingAsync("/login/", cancellation.Token);
        var loginId = loginUrl.Split('/')[^1];
        using var approved = await FormPost.SendAsync(
            _client,
            $"{_origin}/test/approve",
            cancellation.Token,
            ("login_id", loginId));

        Assert.That(approved.IsSuccessStatusCode, Is.True);
        var token = await login;

        Assert.That(token, Is.Not.Null.And.StartWith("test-oat-"));
    }

    [Test]
    public async Task Token_refusesAReplayedAuthorizationCode()
    {
        using var preApproved = await FormPost.SendAsync(
            _client,
            $"{_origin}/test/pre-approve",
            ("client_id", "replay-test"));
        Assert.That(preApproved.IsSuccessStatusCode, Is.True);

        var verifier = PkceVerifier.Secret();
        using var browser = new HttpClient();
        using var page = await browser.GetAsync(
            $"{_origin}/oauth/authorize?response_type=code&client_id=replay-test" +
            $"&code_challenge={Uri.EscapeDataString(PkceVerifier.Challenge(verifier))}&code_challenge_method=S256");
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var latest = await _client.GetAsync($"{_origin}/test/latest-code?client_id=replay-test");
        var code = (await latest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

        Assert.That(await ExchangeAsync(code, verifier), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await ExchangeAsync(code, verifier), Is.EqualTo(HttpStatusCode.BadRequest),
            "An authorization code is single use");
    }

    [Test]
    public async Task Token_refusesAMismatchedPkceVerifier()
    {
        using var preApproved = await FormPost.SendAsync(
            _client,
            $"{_origin}/test/pre-approve",
            ("client_id", "pkce-test"));
        Assert.That(preApproved.IsSuccessStatusCode, Is.True);

        using var browser = new HttpClient();
        using var page = await browser.GetAsync(
            $"{_origin}/oauth/authorize?response_type=code&client_id=pkce-test" +
            $"&code_challenge={Uri.EscapeDataString(PkceVerifier.Challenge(PkceVerifier.Secret()))}&code_challenge_method=S256");
        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var latest = await _client.GetAsync($"{_origin}/test/latest-code?client_id=pkce-test");
        var code = (await latest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;

        Assert.That(await ExchangeAsync(code, PkceVerifier.Secret()), Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Authorize_refusesARequestWithoutPkce()
    {
        using var page = await _client.GetAsync($"{_origin}/oauth/authorize?response_type=code&client_id=no-pkce");

        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    private async Task<HttpStatusCode> ExchangeAsync(string code, string verifier)
    {
        using var response = await FormPost.SendAsync(
            _client,
            $"{_origin}/oauth/token",
            ("grant_type", "authorization_code"),
            ("code", code),
            ("code_verifier", verifier));
        return response.StatusCode;
    }

    private static async Task<string> WaitForUserCodeAsync(AgentOutput output, CancellationToken cancellationToken)
    {
        var line = await output.WaitForLineContainingAsync("one-time code:", cancellationToken);
        return line.Split(':')[^1].Trim();
    }
}
