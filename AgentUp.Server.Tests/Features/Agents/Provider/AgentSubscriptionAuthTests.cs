using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSubscriptionAuthTests
{
    [Test]
    public void KeepSubscription_dropsApiKeyMethodsWithoutTreatingChatGptAsAKey()
    {
        var auth = new AgentSubscriptionAuth();
        var kept = auth.KeepSubscription([
            new AgentAuthMethodDto("chatgpt", "ChatGPT", "subscription"),
            new AgentAuthMethodDto("api-key", "API Key", "token")
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(kept.Select(method => method.Id), Is.EqualTo(new[] { "chatgpt" }));
            Assert.That(auth.IsApiKeyMethod("chatgpt", "ChatGPT"), Is.False);
            Assert.That(auth.IsApiKeyMethod("api-key", "API Key"), Is.True);
        });
    }

    [Test]
    public void Defaults_describeSubscriptionLoginForEachAgent()
    {
        var auth = new AgentSubscriptionAuth();

        Assert.Multiple(() =>
        {
            Assert.That(auth.Defaults(AgentKind.Codex).Single().Id, Is.EqualTo("chatgpt"));
            Assert.That(auth.Defaults(AgentKind.Cursor).Single().Id, Is.EqualTo("cursor_login"));
            Assert.That(auth.Defaults(AgentKind.Claude).Single().Name, Is.EqualTo("Claude Pro"));
            Assert.That(auth.Defaults((AgentKind)99), Is.Empty);
            Assert.That(auth.LooksLikeAuthenticationFailure("Authentication required."), Is.True);
            Assert.That(auth.LooksLikeAuthenticationFailure("spawn failed"), Is.False);
        });
    }
}
