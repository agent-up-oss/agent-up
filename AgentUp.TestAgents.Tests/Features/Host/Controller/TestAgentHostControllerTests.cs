using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Acp.Controllers;
using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Controllers;
using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Controllers;
using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.Host.Services;
using AgentUp.TestAgents.Features.IdentityProvider.Controllers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Host.Controller;

// Every verb the Server can start one of these agents with, driven the way it starts them: over
// stdio, against a real identity provider, with the credential landing where the next launch
// looks for it. Real network and real processes, so these take longer than the 30-second default
// in coverlet.runsettings.
[TestFixture, CancelAfter(120_000)]
public sealed class TestAgentHostControllerTests
{
    private string _home = null!;

    [SetUp]
    public void CreateHome()
    {
        _home = Directory.CreateTempSubdirectory("agent-up-test-agent-host").FullName;
    }

    [TearDown]
    public void RemoveHome()
    {
        try
        {
            Directory.Delete(_home, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TestContext.WriteLine($"Could not remove {_home}: {exception.Message}");
        }
    }

    [Test]
    public async Task RunAsync_servesAcpOverTheStreamsItWasGiven()
    {
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n");
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var command = new TestAgentCommand(TestAgentSchema.PastedCode, TestAgentVerb.Acp, null, 0, null);

        var exitCode = await Host(input, output, errors).RunAsync(command, CancellationToken.None);
        var response = JsonNode.Parse(output.ToString().Trim());

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(response!["result"]!["authMethods"]!.AsArray()[0]!["id"]!.GetValue<string>(),
                Is.EqualTo("claude-login"));
        });
    }

    // The port is the first thing on stdout because a harness that asked for an ephemeral one has
    // no other way to learn which one it got.
    [Test]
    public async Task RunAsync_servesTheIdentityProviderAndReportsThePortOnStdout()
    {
        using var output = new AgentOutput();
        using var errors = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var command = new TestAgentCommand(TestAgentSchema.DeviceCode, TestAgentVerb.IdentityProvider, null, 0, null);

        var serving = Host(TextReader.Null, output, errors).RunAsync(command, lifetime.Token);

        var port = int.Parse(await output.WaitForLineAsync(
            line => line.All(char.IsDigit), "the port it bound", lifetime.Token));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        using var health = await client.GetAsync($"http://localhost:{port}/health", lifetime.Token);
        var payload = await health.Content.ReadFromJsonAsync<JsonElement>(lifetime.Token);

        await lifetime.CancelAsync();
        var exitCode = await serving;

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(port, Is.GreaterThan(0));
            Assert.That(payload.GetProperty("status").GetString(), Is.EqualTo("ok"));
        });
    }

    // The whole point of the credential going under HOME: an agent that signed in once is signed
    // in on the next launch, which is what the Server's HOME redirection relies on.
    [Test]
    public async Task RunAsync_signsInAndLeavesTheCredentialWhereTheNextLaunchLooks()
    {
        await using var provider = new TestIdentityProviderService(0, null);
        provider.Start();
        var origin = $"http://localhost:{provider.Port}";

        using var output = new AgentOutput();
        using var errors = new StringWriter();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var command = new TestAgentCommand(TestAgentSchema.SilentPoll, TestAgentVerb.Login, origin, 0, null);

        var signingIn = Host(TextReader.Null, output, errors).RunAsync(command, lifetime.Token);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var loginUrl = await output.WaitForLineContainingAsync("/login/", lifetime.Token);
        using var approved = await FormPost.SendAsync(
            client,
            $"{origin}/test/approve",
            lifetime.Token,
            ("login_id", loginUrl.Split('/')[^1]));
        Assert.That(approved.IsSuccessStatusCode, Is.True);

        var exitCode = await signingIn;
        var stored = new TestAgentSignInService(_home).Credentials(TestAgentSchema.SilentPoll).Read();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(stored, Is.Not.Null.And.StartWith("test-oat-"));
        });
    }

    // A provider that is not there has to say so and exit, rather than sit until the Server's
    // sign-in deadline expires and report something that says nothing about the cause.
    [Test]
    public async Task RunAsync_reportsAnIdentityProviderItCannotReach()
    {
        using var output = new StringWriter();
        using var errors = new StringWriter();
        var command = new TestAgentCommand(
            TestAgentSchema.SilentPoll, TestAgentVerb.Login, "http://127.0.0.1:1/", 0, null);

        var exitCode = await Host(TextReader.Null, output, errors).RunAsync(command, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(3));
            Assert.That(errors.ToString(), Does.Contain("Could not reach the identity provider"));
        });
    }

    [Test]
    public async Task RunAsync_reportsAnInterruptedSignIn()
    {
        await using var provider = new TestIdentityProviderService(0, null);
        provider.Start();

        using var output = new AgentOutput();
        using var errors = new StringWriter();
        using var lifetime = new CancellationTokenSource();
        var command = new TestAgentCommand(
            TestAgentSchema.SilentPoll, TestAgentVerb.Login, $"http://localhost:{provider.Port}", 0, null);

        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var signingIn = Host(TextReader.Null, output, errors).RunAsync(command, lifetime.Token);
        // Interrupt it only once it is actually waiting, so this covers the sign-in being stopped
        // rather than a token that was cancelled before the work began.
        await output.WaitForLineContainingAsync("/login/", deadline.Token);
        await lifetime.CancelAsync();

        Assert.That(await signingIn, Is.EqualTo(130), "128 plus SIGINT, the way a shell reports it");
    }

    private TestAgentHostController Host(TextReader input, TextWriter output, TextWriter errors) => new(
        new TestAgentHostService(
            new AcpController(new AcpAgentService()),
            new AuthenticationController(new TestAgentSignInService(_home)),
            new IdentityProviderController(new IdentityProviderHostService()),
            new TestAgentConsole(input, output, errors)));
}
