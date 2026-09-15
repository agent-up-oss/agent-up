using AgentUp.TestAgents.Features.Authentication.Providers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Authentication.Provider;

// What each agent does when its sign-in does not work out. These are the paths the Server sees as
// a non-zero exit, and an agent that hung or claimed success here would make a failing sign-in
// look like a Server bug rather than a refused one.
// Real HTTP, so these take longer than the 30-second default in coverlet.runsettings.
[TestFixture, CancelAfter(120_000)]
public sealed class TestAgentLoginFlowRefusalTests
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

    /// <summary>A provider that answers, but not at the paths a sign-in needs.</summary>
    private string Unreachable => $"{_origin}/nowhere";

    [Test]
    public async Task SilentPoll_givesUpWhenTheSignInCannotBeStarted()
    {
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var token = await new SilentPollLoginFlow(_client, Unreachable)
            .RunAsync(output, TextReader.Null, cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Null);
            Assert.That(output.ToString(), Does.Contain("Could not start the sign-in"));
        });
    }

    [Test]
    public async Task DeviceCode_givesUpWhenAuthorizationCannotBeStarted()
    {
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var token = await new DeviceCodeLoginFlow(_client, Unreachable)
            .RunAsync(output, TextReader.Null, cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Null);
            Assert.That(output.ToString(), Does.Contain("Could not start device authorization"));
        });
    }

    // A user who pressed enter without pasting anything: the agent has to say so and stop, not
    // exchange an empty code or wait forever for a second line.
    [Test]
    public async Task PastedCode_givesUpWhenNothingWasPasted()
    {
        using var output = new AgentOutput();
        using var input = new PipedReader();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var login = new PastedCodeLoginFlow(_client, _origin).RunAsync(output, input, cancellation.Token);
        await output.WaitForLineContainingAsync("Paste code here:", cancellation.Token);
        input.Write("   ");
        var token = await login;

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Null);
            Assert.That(output.ToString(), Does.Contain("No code was provided"));
        });
    }

    [Test]
    public async Task PastedCode_reportsACodeTheProviderRefuses()
    {
        using var output = new AgentOutput();
        using var input = new PipedReader();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var login = new PastedCodeLoginFlow(_client, _origin).RunAsync(output, input, cancellation.Token);
        await output.WaitForLineContainingAsync("Paste code here:", cancellation.Token);
        input.Write("not-a-code-this-provider-issued");
        var token = await login;

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Null);
            Assert.That(output.ToString(), Does.Contain("Sign-in failed"));
        });
    }

    // The state is what ties a redirect to the request that started it. A redirect carrying the
    // wrong one is the shape a cross-site attempt takes, and it must not complete a sign-in.
    [Test]
    public async Task LoopbackRedirect_refusesARedirectCarryingTheWrongState()
    {
        using var output = new AgentOutput();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var login = new LoopbackRedirectLoginFlow(_client, _origin).RunAsync(output, TextReader.Null, cancellation.Token);

        var callback = await output.WaitForLineContainingAsync("come back to", cancellation.Token);
        using var browser = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var redirect = await browser.GetAsync($"{callback}?code=whatever&state=not-the-one", cancellation.Token);
        var token = await login;

        Assert.Multiple(() =>
        {
            Assert.That(token, Is.Null);
            Assert.That((int)redirect.StatusCode, Is.EqualTo(400),
                "The browser is told the sign-in did not complete, rather than left hanging");
            Assert.That(output.ToString(), Does.Contain("did not carry a matching state"));
        });
    }
}
