using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSubscriptionLoginParserTests
{
    [Test]
    public void Append_readsACursorLoginLink()
    {
        var parser = new AgentSubscriptionLoginParser(AgentLoginFlow.Poll());
        parser.Append("Failed to open browser for login. Please visit: https://cursor.com/loginDeepControl?challenge=abc&uuid=def");

        Assert.Multiple(() =>
        {
            Assert.That(parser.Challenge.Url, Does.Contain("loginDeepControl"));
            Assert.That(parser.Challenge.Code, Is.Null);
            Assert.That(parser.Challenge.Transport, Is.EqualTo(AgentLoginTransport.Poll));
            Assert.That(parser.Challenge.Instructions, Does.Contain("subscription"));
        });
    }

    [Test]
    public void Append_readsACodexDeviceCodeAndIgnoresApiTokens()
    {
        var parser = new AgentSubscriptionLoginParser(AgentLoginFlow.DeviceCode());
        parser.Append("Follow these steps to sign in with ChatGPT using device code ABCD-EFGHI:");
        parser.Append("https://auth.openai.com/codex/device");
        parser.Append("Enter this one-time code");
        parser.Append("ABCD-EFGHI");
        parser.Append("sk-ant-api03-not-a-subscription");
        parser.Append("sk-ant-oat01-real-token");

        Assert.Multiple(() =>
        {
            Assert.That(parser.Challenge.Url, Is.EqualTo("https://auth.openai.com/codex/device"));
            Assert.That(parser.Challenge.Code, Is.EqualTo("ABCD-EFGHI"));
            Assert.That(parser.Challenge.Transport, Is.EqualTo(AgentLoginTransport.Code));
            Assert.That(parser.ClaudeOAuthToken, Is.EqualTo("sk-ant-oat01-real-token"));
        });
    }

    // Colour and cursor control sequences are normal in these CLIs, and a link wrapped in them
    // used to come back with escape bytes still attached.
    [Test]
    public void Append_stripsTerminalEscapeSequencesFromTheLink()
    {
        var parser = new AgentSubscriptionLoginParser(AgentLoginFlow.Poll());
        parser.Append("\u001b[2K\u001b[36mPlease visit: \u001b[4mhttps://cursor.com/loginDeepControl?uuid=def\u001b[0m");

        Assert.That(parser.Challenge.Url, Is.EqualTo("https://cursor.com/loginDeepControl?uuid=def"));
    }

    // A polling sign-in carries nothing back, so a code-shaped token in a progress message must
    // not turn into a code the client asks the user to type.
    [Test]
    public void Append_doesNotAdoptACodeShapedTokenForAFlowThatCarriesNoCode()
    {
        var parser = new AgentSubscriptionLoginParser(AgentLoginFlow.Poll());
        parser.Append("Visit https://cursor.com/loginDeepControl?uuid=def");
        parser.Append("Tracking code ABCD-EFGHI for this login attempt");

        Assert.That(parser.Challenge.Code, Is.Null);
    }

    [Test]
    public void Append_marksTheChallengeSubmittableOnlyOnceThePromptArrives()
    {
        var parser = new AgentSubscriptionLoginParser(AgentLoginFlow.PastedCode());
        parser.Append("Visit https://claude.ai/oauth/authorize?code=true");
        Assert.That(parser.Challenge.CanSubmitCode, Is.False, "Nothing is waiting on stdin yet");

        parser.Append("Paste code here: ");

        Assert.Multiple(() =>
        {
            Assert.That(parser.AwaitingCodeInput, Is.True);
            Assert.That(parser.Challenge.CanSubmitCode, Is.True);
            Assert.That(parser.Challenge.Instructions, Does.Contain("paste"));
        });
    }

    [Test]
    public void ReadRedirectUri_liftsTheLoopbackCallbackOutOfTheAuthorizationLink()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AgentSubscriptionLoginParser.ReadRedirectUri(
                    "https://auth.openai.com/oauth/authorize?client_id=x&redirect_uri=http%3A%2F%2Flocalhost%3A1455%2Fauth%2Fcallback"),
                Is.EqualTo("http://localhost:1455/auth/callback"));
            Assert.That(AgentSubscriptionLoginParser.ReadRedirectUri("https://cursor.com/loginDeepControl?uuid=def"), Is.Null);
            Assert.That(AgentSubscriptionLoginParser.ReadRedirectUri("not a url"), Is.Null);
        });
    }

    [Test]
    public void ReadExpiry_readsAStatedLifetime()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AgentSubscriptionLoginParser.ReadExpiry("This code expires in 15 minutes"), Is.Not.Null);
            Assert.That(AgentSubscriptionLoginParser.ReadExpiry("expires in 90 seconds"), Is.Not.Null);
            Assert.That(AgentSubscriptionLoginParser.ReadExpiry("no lifetime stated"), Is.Null);
        });
    }

    [Test]
    public void IsCodePrompt_recognisesAPromptAndIgnoresOrdinaryOutput()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AgentSubscriptionLoginParser.IsCodePrompt("Paste code here: "), Is.True);
            Assert.That(AgentSubscriptionLoginParser.IsCodePrompt("Authorization code?"), Is.True);
            Assert.That(AgentSubscriptionLoginParser.IsCodePrompt("Waiting for the code to be entered"), Is.False);
            Assert.That(AgentSubscriptionLoginParser.IsCodePrompt(string.Empty), Is.False);
        });
    }

    [Test]
    public void ChooseUrl_keepsANonPreferredLinkWhenNoVendorLoginUrlIsPresent()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                AgentSubscriptionLoginParser.ChooseUrl("See https://example.com/docs. and https://claude.ai/login"),
                Is.EqualTo("https://claude.ai/login"));
            Assert.That(
                AgentSubscriptionLoginParser.ChooseUrl("Open https://example.com/help."),
                Is.EqualTo("https://example.com/help"));
            Assert.That(AgentSubscriptionLoginParser.ChooseUrl("no link here"), Is.Null);
            Assert.That(
                AgentSubscriptionLoginParser.Instructions(AgentLoginFlow.Poll(), null, null),
                Does.Contain("sign-in link"));
        });
    }
}
