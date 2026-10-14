using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class JsonAgentWorkspaceSettingsRepositoryTests
{
    [Test]
    public void Save_roundTripsPolicyRoleAndHostAuthorization()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-settings").FullName;
        var first = new JsonAgentWorkspaceSettingsRepository(directory);
        var role = new AgentRoleDefinition("Ship it", ["read_file"], RoleTriggerKind.IssueAssigned, RoleTerminationKind.ProposalOpened);
        first.Save(new AgentWorkspaceSettings(
            "ws-1",
            [new PermissionRule("write", PermissionRuleKind.WorktreeWrite, true)],
            role,
            false,
            true,
            AssignedIssueOrder.CreatedAsc,
            null));

        var loaded = new JsonAgentWorkspaceSettingsRepository(directory).Get("ws-1");

        Assert.Multiple(() =>
        {
            Assert.That(loaded.HostConnectionAuthorized, Is.True);
            Assert.That(loaded.QueueOrder, Is.EqualTo(AssignedIssueOrder.CreatedAsc));
            Assert.That(loaded.PolicyRules, Has.Count.EqualTo(1));
            Assert.That(loaded.Role!.Prompt, Is.EqualTo("Ship it"));
        });
    }

    [Test]
    public void Remove_dropsTheWorkspaceFromLaterLoads()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-settings").FullName;
        var repository = new JsonAgentWorkspaceSettingsRepository(directory);
        repository.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws-1") with { HostConnectionAuthorized = true });
        repository.Remove("ws-1");

        Assert.That(new JsonAgentWorkspaceSettingsRepository(directory).Get("ws-1").HostConnectionAuthorized, Is.False);
    }
}
