using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSubscriptionLoginParserTests
{
    [Test]
    public void Append_readsACursorLoginLink()
    {
        var parser = new AgentSubscriptionLoginParser();
        parser.Append("Failed to open browser for login. Please visit: https://cursor.com/loginDeepControl?challenge=abc&uuid=def");

        Assert.Multiple(() =>
        {
            Assert.That(parser.Challenge.Url, Does.Contain("loginDeepControl"));
            Assert.That(parser.Challenge.Code, Is.Null);
            Assert.That(parser.Challenge.Instructions, Does.Contain("subscription"));
        });
    }

    [Test]
    public void Append_readsACodexDeviceCodeAndIgnoresApiTokens()
    {
        var parser = new AgentSubscriptionLoginParser();
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
            Assert.That(parser.ClaudeOAuthToken, Is.EqualTo("sk-ant-oat01-real-token"));
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
            Assert.That(AgentSubscriptionLoginParser.Instructions(null, null), Does.Contain("sign-in link"));
        });
    }
}
