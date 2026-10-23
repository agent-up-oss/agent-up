using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class HostConnectionPresenceProviderTests
{
    [Test]
    public void IsPresent_isFalseUntilAWorkspaceHostIsAuthorized()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var host = new HostConnectionPresenceProvider(settings);

        Assert.That(host.IsPresent(), Is.False);
        Assert.That(host.IsPresent("ws"), Is.False);
    }

    [Test]
    public void IsPresent_readsTheWorkspaceAuthorizationFlag()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws") with { HostConnectionAuthorized = true });
        var host = new HostConnectionPresenceProvider(settings);

        Assert.Multiple(() =>
        {
            Assert.That(host.IsPresent(), Is.True);
            Assert.That(host.IsPresent("ws"), Is.True);
            Assert.That(host.IsPresent("other"), Is.False);
        });
    }
}
