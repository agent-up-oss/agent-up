using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentLoginFlowProviderTests
{
    [Test]
    public void Resolve_defaultsMatchTheLoginCommandEachKindActuallyRuns()
    {
        var provider = new AgentLoginFlowProvider(Configuration());

        Assert.Multiple(() =>
        {
            // codex login --device-auth prints a code and polls; nothing comes back over stdin.
            var codex = provider.Resolve(AgentKind.Codex);
            Assert.That(codex.Transport, Is.EqualTo(AgentLoginTransport.Code));
            Assert.That(codex.NeedsCodeInput, Is.False);

            // cursor login polls on its own once the link is open.
            Assert.That(provider.Resolve(AgentKind.Cursor).Transport, Is.EqualTo(AgentLoginTransport.Poll));

            // claude setup-token blocks on stdin for a pasted code.
            var claude = provider.Resolve(AgentKind.Claude);
            Assert.That(claude.Transport, Is.EqualTo(AgentLoginTransport.Code));
            Assert.That(claude.NeedsCodeInput, Is.True);
        });
    }

    [Test]
    public void Resolve_honoursAConfiguredTransport()
    {
        var provider = new AgentLoginFlowProvider(Configuration(("Agents:Codex:LoginTransport", "redirect")));

        Assert.That(provider.Resolve(AgentKind.Codex).Transport, Is.EqualTo(AgentLoginTransport.Redirect));
    }

    [Test]
    public void Resolve_honoursConfiguredTimeouts()
    {
        var provider = new AgentLoginFlowProvider(Configuration(
            ("Agents:Cursor:LoginChallengeTimeoutSeconds", "5"),
            ("Agents:Cursor:LoginCompletionTimeoutSeconds", "30")));

        var flow = provider.Resolve(AgentKind.Cursor);

        Assert.Multiple(() =>
        {
            Assert.That(flow.ChallengeTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
            Assert.That(flow.CompletionTimeout, Is.EqualTo(TimeSpan.FromSeconds(30)));
        });
    }

    [Test]
    public void Resolve_rejectsATransportOrTimeoutItCannotHonour()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => new AgentLoginFlowProvider(Configuration(("Agents:Codex:LoginTransport", "carrier-pigeon"))).Resolve(AgentKind.Codex),
                Throws.InvalidOperationException.With.Message.Contains("not a supported login transport"));
            Assert.That(
                () => new AgentLoginFlowProvider(Configuration(("Agents:Codex:LoginChallengeTimeoutSeconds", "0"))).Resolve(AgentKind.Codex),
                Throws.InvalidOperationException.With.Message.Contains("positive number of seconds"));
            Assert.That(
                () => new AgentLoginFlowProvider(Configuration(("Agents:Codex:LoginCompletionTimeoutSeconds", "soon"))).Resolve(AgentKind.Codex),
                Throws.InvalidOperationException.With.Message.Contains("positive number of seconds"));
        });
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] settings)
    {
        var values = settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
