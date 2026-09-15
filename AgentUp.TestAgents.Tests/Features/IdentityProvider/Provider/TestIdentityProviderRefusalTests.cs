using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.IdentityProvider.Provider;

// What the provider does when it is asked for something it should refuse. A test agent that
// mis-handles a refusal would look like a Server bug when the suites run, so the shapes the real
// providers return are pinned here rather than only on the happy path.
// Real HTTP, so these take longer than the 30-second default in coverlet.runsettings.
[TestFixture, CancelAfter(120_000)]
public sealed class TestIdentityProviderRefusalTests
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
        // Redirects are followed by default, and several of these assert on the redirect itself.
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
    public async Task Authorize_refusesARequestWithoutAClient()
    {
        using var page = await _client.GetAsync($"{_origin}/oauth/authorize?response_type=code&code_challenge_method=S256");

        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Authorize_showsTheConsentPageWhenTheClientWasNotPreApproved()
    {
        using var page = await _client.GetAsync(
            $"{_origin}/oauth/authorize?response_type=code&client_id=not-pre-approved" +
            "&code_challenge=abc&code_challenge_method=S256&state=xyz");
        var body = await page.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("id=\"approve\""), "A user has to be able to approve it");
            Assert.That(body, Does.Contain("name=\"state\""), "State has to survive the round trip");
        });
    }

    // Approving by hand is the path a user takes when nothing pre-approved the client, and with
    // no redirect_uri the code is shown to be carried back by hand, the way setup-token does it.
    [Test]
    public async Task Approve_withoutARedirectShowsTheCodeToCarryBack()
    {
        using var page = await FormPost.SendAsync(
            _client, $"{_origin}/oauth/approve", ("client_id", "carried-by-hand"), ("state", "xyz"));
        var body = await page.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("id=\"code\""));
            Assert.That(body, Does.Contain("#xyz"), "The state rides along so the agent can check it");
        });
    }

    [Test]
    public async Task Approve_refusesWithoutAClient()
    {
        using var page = await FormPost.SendAsync(_client, $"{_origin}/oauth/approve", ("state", "xyz"));

        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Approve_withARedirectSendsTheCodeAndStateBackToIt()
    {
        using var page = await FormPost.SendAsync(
            _client,
            $"{_origin}/oauth/approve",
            ("client_id", "redirected"),
            ("redirect_uri", "http://127.0.0.1:1/auth/callback"),
            ("state", "xyz"));

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(page.Headers.Location!.ToString(), Does.Contain("code="));
            Assert.That(page.Headers.Location!.ToString(), Does.Contain("state=xyz"));
        });
    }

    [Test]
    public async Task Token_refusesARequestCarryingNoCode()
    {
        using var response = await FormPost.SendAsync(
            _client, $"{_origin}/oauth/token", ("grant_type", "authorization_code"));
        var error = await ErrorOf(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(error, Is.EqualTo("invalid_request"));
        });
    }

    [Test]
    public async Task DeviceToken_refusesADeviceCodeItNeverIssued()
    {
        using var response = await FormPost.SendAsync(
            _client, $"{_origin}/oauth/device/token", ("client_id", "test-agent2"), ("device_code", "never-issued"));
        var error = await ErrorOf(response);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(error, Is.EqualTo("invalid_grant"));
        });
    }

    [Test]
    public async Task DeviceEntry_offersThePageTheUserCodeIsTypedInto()
    {
        using var page = await _client.GetAsync($"{_origin}/device");
        var body = await page.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("id=\"user-code\""));
        });
    }

    [Test]
    public async Task DeviceApprove_refusesACodeNobodyIsWaitingOn()
    {
        using var page = await FormPost.SendAsync(_client, $"{_origin}/device/approve", ("user_code", "ZZZZ-ZZZZ"));

        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    // Approving through the page is the path a person takes; the suites use the control plane
    // instead, so without this the page's own approval would never run.
    [Test]
    public async Task DeviceApprove_acceptsACodeThatWasIssued()
    {
        using var started = await FormPost.SendAsync(
            _client, $"{_origin}/oauth/device/code", ("client_id", "device-page"));
        var userCode = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user_code").GetString();

        using var page = await FormPost.SendAsync(_client, $"{_origin}/device/approve", ("user_code", userCode!));
        var body = await page.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Contain("id=\"status\""));
        });
    }

    [Test]
    public async Task Login_reportsASignInItNeverStarted()
    {
        using var page = await _client.GetAsync($"{_origin}/login/never-started");
        using var approve = await FormPost.SendAsync(_client, $"{_origin}/login/never-started/approve");
        using var status = await _client.GetAsync($"{_origin}/login/never-started/status");
        var payload = await status.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(approve.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(status.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(payload.GetProperty("status").GetString(), Is.EqualTo("unknown"));
        });
    }

    [Test]
    public async Task Login_approvedThroughItsOwnPageReadsAsApproved()
    {
        using var started = await FormPost.SendAsync(_client, $"{_origin}/login/start", ("client_id", "test-agent4"));
        var loginId = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("login_id").GetString();

        using var page = await _client.GetAsync($"{_origin}/login/{loginId}");
        using var pending = await _client.GetAsync($"{_origin}/login/{loginId}/status");
        var before = await pending.Content.ReadFromJsonAsync<JsonElement>();

        using var approved = await FormPost.SendAsync(_client, $"{_origin}/login/{loginId}/approve");
        using var status = await _client.GetAsync($"{_origin}/login/{loginId}/status");
        var after = await status.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(before.GetProperty("status").GetString(), Is.EqualTo("pending"));
            Assert.That(approved.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(after.GetProperty("status").GetString(), Is.EqualTo("approved"));
            Assert.That(after.GetProperty("access_token").GetString(), Does.StartWith("test-oat-"));
        });
    }

    [Test]
    public async Task PreApprove_refusesWithoutAClient()
    {
        using var response = await FormPost.SendAsync(_client, $"{_origin}/test/pre-approve");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Approve_throughTheControlPlaneReportsWhenThereIsNothingToApprove()
    {
        using var byUserCode = await FormPost.SendAsync(
            _client, $"{_origin}/test/approve", ("user_code", "ZZZZ-ZZZZ"));
        using var byLoginId = await FormPost.SendAsync(
            _client, $"{_origin}/test/approve", ("login_id", "never-started"));
        using var byNothing = await FormPost.SendAsync(_client, $"{_origin}/test/approve");

        Assert.Multiple(() =>
        {
            Assert.That(byUserCode.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(byLoginId.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(byNothing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
                "Approving nothing in particular is a mistake, not a refusal");
        });
    }

    [Test]
    public async Task LatestCode_reportsNothingForAClientThatNeverAuthorized()
    {
        using var response = await _client.GetAsync($"{_origin}/test/latest-code?client_id=never-authorized");
        using var unnamed = await _client.GetAsync($"{_origin}/test/latest-code");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(unnamed.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
    }

    [Test]
    public async Task AnUnknownPath_isNotFound()
    {
        using var page = await _client.GetAsync($"{_origin}/nowhere");

        Assert.That(page.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    // The origin is what every link the agents print is built from, and it is not known until the
    // listener has bound. Reading it early has to say so rather than hand back a wrong one.
    [Test]
    public async Task ItsOrigin_isNotAvailableBeforeItHasBound()
    {
        await using var unstarted = new TestIdentityProviderService(0, null);

        Assert.Multiple(() =>
        {
            Assert.That(unstarted.Port, Is.Zero);
            Assert.That(() => unstarted.PublicOrigin, Throws.InstanceOf<InvalidOperationException>());
        });
    }

    private static async Task<string?> ErrorOf(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.TryGetProperty("error", out var value) ? value.GetString() : null;
    }
}
