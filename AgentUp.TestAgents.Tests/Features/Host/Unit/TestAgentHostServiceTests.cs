using AgentUp.TestAgents.Features.Acp.Controllers;
using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Controllers;
using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.Host.Services;
using AgentUp.TestAgents.Features.IdentityProvider.Controllers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;
using AgentUp.TestAgents.Features.Inference.Controllers;
using AgentUp.TestAgents.Features.Inference.Providers;
using AgentUp.TestAgents.Features.Inference.Services;
using AgentUp.TestAgents.Features.Scripted.Controllers;
using AgentUp.TestAgents.Features.Scripted.Providers;
using AgentUp.TestAgents.Features.Scripted.Services;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Host.Unit;

[TestFixture]
public sealed class TestAgentHostServiceTests
{
    // A misconfigured agent has to fail loudly and immediately. Left to hang instead, it would
    // surface much later as the Server's sign-in deadline expiring, which says nothing about the
    // actual cause.
    [Test]
    public async Task RunAsync_failsImmediatelyWhenNoIdentityProviderIsConfigured()
    {
        var command = new TestAgentCommand(TestAgentSchema.DeviceCode, TestAgentVerb.Login, null, 0, null);

        var exitCode = await Host().RunAsync(command, CancellationToken.None);

        Assert.That(exitCode, Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_treatsABlankIdentityProviderAsMissing()
    {
        var command = new TestAgentCommand(TestAgentSchema.PastedCode, TestAgentVerb.Login, "   ", 0, null);

        var exitCode = await Host().RunAsync(command, CancellationToken.None);

        Assert.That(exitCode, Is.EqualTo(2));
    }

    [Test]
    public async Task RunAsync_servesLocalInferenceWithoutAnIdentityProvider()
    {
        var command = new TestAgentCommand(TestAgentSchema.LocalInference, TestAgentVerb.Acp, null, 0, null);
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var host = new TestAgentHostService(
            new AcpController(new AcpAgentService()),
            new AuthenticationController(new TestAgentSignInService()),
            new IdentityProviderController(new IdentityProviderHostService()),
            new InferenceAcpController(
                new LocalInferenceAcpService(
                    new ScriptedTokenGenerator(),
                    new ToolCallParser(),
                    new WorktreeToolExecutor(),
                    new FixedLoopInferenceSettings(42))),
            new ScriptedAcpController(new ScriptedAcpService(new EnvironmentScriptedTurnScript())),
            new TestAgentConsole(input, output, error));

        var exitCode = await host.RunAsync(command, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("authMethods"));
            Assert.That(error.ToString(), Does.Contain("loop-inference seed=42"));
        });
    }

    [Test]
    public async Task RunAsync_servesScriptedWithoutAnIdentityProvider()
    {
        var command = new TestAgentCommand(TestAgentSchema.Scripted, TestAgentVerb.Acp, null, 0, null);
        using var input = new StringReader(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}""" + "\n"
            + """{"jsonrpc":"2.0","id":2,"method":"session/prompt","params":{}}""" + "\n");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var host = new TestAgentHostService(
            new AcpController(new AcpAgentService()),
            new AuthenticationController(new TestAgentSignInService()),
            new IdentityProviderController(new IdentityProviderHostService()),
            new InferenceAcpController(
                new LocalInferenceAcpService(
                    new ScriptedTokenGenerator(),
                    new ToolCallParser(),
                    new WorktreeToolExecutor(),
                    new FixedLoopInferenceSettings(42))),
            new ScriptedAcpController(new ScriptedAcpService(new EnvironmentScriptedTurnScript())),
            new TestAgentConsole(input, output, error));

        var exitCode = await host.RunAsync(command, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(output.ToString(), Does.Contain("authMethods"));
            Assert.That(output.ToString(), Does.Contain("end_turn"));
        });
    }

    [Test]
    public void Command_carriesTheSchemaVerbAndProviderItWasAskedFor()
    {
        var command = new TestAgentCommand(TestAgentSchema.SilentPoll, TestAgentVerb.Acp, "http://localhost:9000", 9000, "http://10.0.2.2:9000");

        Assert.Multiple(() =>
        {
            Assert.That(command.Schema, Is.EqualTo(TestAgentSchema.SilentPoll));
            Assert.That(command.Verb, Is.EqualTo(TestAgentVerb.Acp));
            Assert.That(command.IdentityProviderUrl, Is.EqualTo("http://localhost:9000"));
            // The origin a client reaches differs from the one the provider bound, because an
            // Android emulator reaches its host as 10.0.2.2.
            Assert.That(command.PublicOrigin, Is.EqualTo("http://10.0.2.2:9000"));
        });
    }

    // HttpClient.Timeout throws TaskCanceledException. That used to be reported as SIGINT (130),
    // which is what the iOS device-code suite waited out after a slow identity-provider poll.
    [Test]
    public async Task RunAsync_reportsAnIdentityProviderTimeoutAsUnreachableRatherThanInterrupted()
    {
        using var errors = new StringWriter();
        using var client = new HttpClient(new TimeoutHandler());
        var command = new TestAgentCommand(TestAgentSchema.SilentPoll, TestAgentVerb.Login, "http://idp.test", 0, null);

        var exitCode = await Host(errors, client).RunAsync(command, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(3));
            Assert.That(errors.ToString(), Does.Contain("Could not reach the identity provider"));
        });
    }

    /// <summary>The host as the entrypoint composes it, reaching each slice through its controller.</summary>
    private static TestAgentHostService Host() => Host(TextWriter.Null, signInClient: null);

    private static TestAgentHostService Host(TextWriter errors, HttpClient? signInClient) => new(
        new AcpController(new AcpAgentService()),
        new AuthenticationController(new TestAgentSignInService()),
        new IdentityProviderController(new IdentityProviderHostService()),
        new InferenceAcpController(
            new LocalInferenceAcpService(
                new ScriptedTokenGenerator(),
                new ToolCallParser(),
                new WorktreeToolExecutor(),
                new FixedLoopInferenceSettings(42))),
        new ScriptedAcpController(new ScriptedAcpService(new EnvironmentScriptedTurnScript())),
        new TestAgentConsole(TextReader.Null, TextWriter.Null, errors),
        signInClient);

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout elapsing.");
    }
}
