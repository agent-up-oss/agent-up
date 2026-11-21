using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class FirstPartyCapabilityCatalogTests
{
    [Test]
    public void The_server_is_built_with_every_first_party_capability()
    {
        var ids = new FirstPartyCapabilityCatalog().Modules.Select(module => module.Identity.Id);

        Assert.That(ids, Is.EqualTo(new[] { "dotnet", "docker", "codex", "cursor", "claude", "opencode", "github", "forgejo" }));
    }

    [Test]
    public void OpenCode_is_an_agent_that_a_permission_policy_can_govern()
    {
        var opencode = new FirstPartyCapabilityCatalog().Modules.OfType<IAgentCapability>().Single(module => module.Identity.Id == "opencode");

        Assert.That(opencode.Kind, Is.EqualTo(CapabilityKind.Agent));
        Assert.That(opencode.SupportsPermissionRequests, Is.True);
        Assert.That(opencode.Settings.Select(setting => setting.Key), Is.EquivalentTo(new[] { "endpoint", "token", "model" }));
    }

    [Test]
    public void Every_capability_kind_is_one_of_the_closed_set()
    {
        Assert.That(new FirstPartyCapabilityCatalog().Modules.Select(module => module.Kind), Is.All.AnyOf("runtime", "agent", "forge"));
    }
}
