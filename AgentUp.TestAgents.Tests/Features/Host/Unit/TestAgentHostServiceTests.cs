using AgentUp.TestAgents.Features.Acp.Controllers;
using AgentUp.TestAgents.Features.Acp.Services;
using AgentUp.TestAgents.Features.Authentication.Controllers;
using AgentUp.TestAgents.Features.Authentication.Services;
using AgentUp.TestAgents.Features.Host.Models;
using AgentUp.TestAgents.Features.Host.Services;
using AgentUp.TestAgents.Features.IdentityProvider.Controllers;
using AgentUp.TestAgents.Features.IdentityProvider.Services;

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

    /// <summary>The host as the entrypoint composes it, reaching each slice through its controller.</summary>
    private static TestAgentHostService Host() => new(
        new AcpController(new AcpAgentService()),
        new AuthenticationController(new TestAgentSignInService()),
        new IdentityProviderController(new IdentityProviderHostService()),
        new TestAgentConsole(TextReader.Null, TextWriter.Null, TextWriter.Null));
}
