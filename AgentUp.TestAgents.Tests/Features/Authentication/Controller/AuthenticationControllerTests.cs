using AgentUp.TestAgents.Features.Authentication.Controllers;
using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Authentication.Controller;

// Real HTTP against the real provider, through the boundary the host actually calls. Nothing is
// stubbed: if the controller routes a schema to the wrong flow, these fail.
// Real network, so these take longer than the 30-second default in coverlet.runsettings.
[TestFixture, CancelAfter(120_000)]
public sealed class AuthenticationControllerTests
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

    [Test]
    public async Task SignInAsync_runsTheSignInTheAgentImplementsAndReturnsItsToken()
    {
        var controller = new AuthenticationController(new TestAgentSignInService());
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var login = controller.SignInAsync(
            TestAgentSchema.SilentPoll, _client, _origin, output, TextReader.Null, cancellation.Token);

        // Approve through the provider's control plane rather than driving a browser, which is
        // what keeps this bounded by a deadline instead of by a poll interval.
        var loginUrl = await output.WaitForLineContainingAsync("/login/", cancellation.Token);
        using var approved = await _client.PostAsync(
            $"{_origin}/test/approve",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("login_id", loginUrl.Split('/')[^1])]),
            cancellation.Token);
        Assert.That(approved.IsSuccessStatusCode, Is.True);

        Assert.That(await login, Is.Not.Null.And.StartWith("test-oat-"));
    }

    // Each agent presents its own client id, and the provider tells them apart by it. A schema
    // routed to the wrong flow would sign a different agent in and still return a token.
    [Test]
    public async Task SignInAsync_signsInAsTheAgentTheSchemaNames()
    {
        using var preApproved = await _client.PostAsync(
            $"{_origin}/test/pre-approve",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("client_id", "test-agent1")]));
        Assert.That(preApproved.IsSuccessStatusCode, Is.True);

        var controller = new AuthenticationController(new TestAgentSignInService());
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var login = controller.SignInAsync(
            TestAgentSchema.LoopbackRedirect, _client, _origin, output, TextReader.Null, cancellation.Token);

        // Stand in for the browser: following the link redirects onto the agent's own listener.
        var authorizeUrl = await output.WaitForLineContainingAsync("/oauth/authorize", cancellation.Token);
        Assert.That(authorizeUrl, Does.Contain("client_id=test-agent1"));
        using var browser = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var page = await browser.GetAsync(authorizeUrl, cancellation.Token);
        Assert.That(page.IsSuccessStatusCode, Is.True);

        Assert.That(await login, Is.Not.Null.And.StartWith("test-oat-"));
    }

    [Test]
    public void Credentials_handsBackTheStoreTheAgentKeepsItsTokenIn()
    {
        var controller = new AuthenticationController(new TestAgentSignInService());

        Assert.That(controller.Credentials(TestAgentSchema.DeviceCode), Is.Not.Null);
    }
}
