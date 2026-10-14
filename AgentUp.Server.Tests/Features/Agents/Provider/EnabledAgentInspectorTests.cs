using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class EnabledAgentInspectorTests
{
    [Test]
    public void SupportsPermissionRequests_defaultsTrueWhenTheModuleIsUnknown()
    {
        Assert.That(new EnabledAgentInspector().SupportsPermissionRequests("missing"), Is.True);
    }

    [Test]
    public void SupportsPermissionRequests_readsTheEnabledModuleFlag()
    {
        var packages = FakeEnabledCapabilityPackages.FirstParty()
            .WithAgents(new StubAgentCapability { SupportsPermissionRequests = false });
        var inspector = new EnabledAgentInspector(packages);

        Assert.That(inspector.SupportsPermissionRequests("codex"), Is.False);
    }
}
