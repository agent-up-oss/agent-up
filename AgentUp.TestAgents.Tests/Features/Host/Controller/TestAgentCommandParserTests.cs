using AgentUp.TestAgents.Features.Host.Controllers;
using AgentUp.TestAgents.Features.Host.Models;

namespace AgentUp.TestAgents.Tests.Features.Host.Controller;

[TestFixture]
public sealed class TestAgentCommandParserTests
{
    [TearDown]
    public void ClearOverrides()
    {
        Environment.SetEnvironmentVariable("AGENTUP_TEST_AGENT", null);
        Environment.SetEnvironmentVariable("AGENTUP_TEST_IDP_URL", null);
    }

    [Test]
    public void ReadSchema_mapsEachPublishedNameToItsSignInShape()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestAgentCommandParser.ReadSchema("test-agent1"), Is.EqualTo(TestAgentSchema.LoopbackRedirect));
            Assert.That(TestAgentCommandParser.ReadSchema("test-agent2"), Is.EqualTo(TestAgentSchema.DeviceCode));
            Assert.That(TestAgentCommandParser.ReadSchema("test-agent3"), Is.EqualTo(TestAgentSchema.PastedCode));
            Assert.That(TestAgentCommandParser.ReadSchema("test-agent4"), Is.EqualTo(TestAgentSchema.SilentPoll));
            Assert.That(TestAgentCommandParser.ReadSchema("/opt/agents/test-agent2.exe"), Is.EqualTo(TestAgentSchema.DeviceCode));
            Assert.That(
                () => TestAgentCommandParser.ReadSchema("some-other-binary"),
                Throws.InvalidOperationException.With.Message.Contains("is not a test agent"));
        });
    }

    // The name has to come from something explicit. A renamed or symlinked apphost does not
    // reliably report the name it was launched under, and guessing it would be a flake source.
    [Test]
    public void ResolveName_prefersTheExplicitNameOverTheExecutablePath()
    {
        Environment.SetEnvironmentVariable("AGENTUP_TEST_AGENT", "test-agent4");

        Assert.Multiple(() =>
        {
            Assert.That(
                TestAgentCommandParser.ResolveName("/opt/agent-up-test-agent", ["--agent", "test-agent2"]),
                Is.EqualTo("test-agent2"),
                "An explicit argument wins");
            Assert.That(
                TestAgentCommandParser.ResolveName("/opt/agent-up-test-agent", []),
                Is.EqualTo("test-agent4"),
                "Then the environment the shim sets");
        });
    }

    [Test]
    public void ReadVerb_recognisesBothSpellingsTheServerUses()
    {
        Assert.Multiple(() =>
        {
            // codex and cursor spell it login; claude spells it setup-token.
            Assert.That(TestAgentCommandParser.ReadVerb(["login"]), Is.EqualTo(TestAgentVerb.Login));
            Assert.That(TestAgentCommandParser.ReadVerb(["setup-token"]), Is.EqualTo(TestAgentVerb.Login));
            Assert.That(TestAgentCommandParser.ReadVerb(["login", "--device-auth"]), Is.EqualTo(TestAgentVerb.Login));
            Assert.That(TestAgentCommandParser.ReadVerb(["acp"]), Is.EqualTo(TestAgentVerb.Acp));
            Assert.That(TestAgentCommandParser.ReadVerb([]), Is.EqualTo(TestAgentVerb.Acp), "ACP is the default");
        });
    }

    [Test]
    public void Parse_readsTheIdentityProviderAndPublicOrigin()
    {
        var command = TestAgentCommandParser.Parse(
            "test-agent2",
            ["login", "--idp", "http://127.0.0.1:9001", "--public-origin", "http://10.0.2.2:9001", "--port", "9001"]);

        Assert.Multiple(() =>
        {
            Assert.That(command.Schema, Is.EqualTo(TestAgentSchema.DeviceCode));
            Assert.That(command.Verb, Is.EqualTo(TestAgentVerb.Login));
            Assert.That(command.IdentityProviderUrl, Is.EqualTo("http://127.0.0.1:9001"));
            // An Android emulator reaches the host as 10.0.2.2, so links must be written with the
            // origin the client can open rather than the one the provider bound.
            Assert.That(command.PublicOrigin, Is.EqualTo("http://10.0.2.2:9001"));
            Assert.That(command.Port, Is.EqualTo(9001));
        });
    }

    [Test]
    public void Parse_treatsTheIdentityProviderNameAsItsOwnVerb()
    {
        var command = TestAgentCommandParser.Parse("test-idp", []);

        Assert.That(command.Verb, Is.EqualTo(TestAgentVerb.IdentityProvider));
    }

    [Test]
    public void ReadOption_acceptsBothSeparatedAndJoinedForms()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestAgentCommandParser.ReadOption(["--idp", "http://x"], "--idp"), Is.EqualTo("http://x"));
            Assert.That(TestAgentCommandParser.ReadOption(["--idp=http://y"], "--idp"), Is.EqualTo("http://y"));
            Assert.That(TestAgentCommandParser.ReadOption(["--idp"], "--idp"), Is.Null, "A trailing flag has no value");
            Assert.That(TestAgentCommandParser.ReadOption([], "--idp"), Is.Null);
        });
    }

    [Test]
    public void ReadPort_fallsBackToAnEphemeralPortWhenItIsNotUsable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestAgentCommandParser.ReadPort(["--port", "9001"]), Is.EqualTo(9001));
            Assert.That(TestAgentCommandParser.ReadPort(["--port", "0"]), Is.Zero);
            Assert.That(TestAgentCommandParser.ReadPort(["--port", "99999"]), Is.Zero);
            Assert.That(TestAgentCommandParser.ReadPort(["--port", "not-a-port"]), Is.Zero);
        });
    }
}
