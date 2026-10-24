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

    [Test]
    public void SetServerEnabled_survivesANewRepositoryInstance()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-autonomy-settings").FullName;
        var first = new JsonAgentWorkspaceSettingsRepository(directory);
        first.SetServerEnabled(true);

        var loaded = new JsonAgentWorkspaceSettingsRepository(directory);

        Assert.That(loaded.ServerEnabled, Is.True);
        loaded.SetServerEnabled(false);
        Assert.That(new JsonAgentWorkspaceSettingsRepository(directory).ServerEnabled, Is.False);
    }

    [Test]
    public void LoadAutonomy_readsEnabledTrueFromTheSiblingFile()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-autonomy-load").FullName;
        File.WriteAllText(Path.Join(directory, "autonomy-settings.json"), """{"enabled":true}""");

        Assert.That(new JsonAgentWorkspaceSettingsRepository(directory).ServerEnabled, Is.True);
    }

    [Test]
    public void LoadAutonomy_staysDisabledWhenEnabledIsMissingOrNotTrue()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-autonomy-false").FullName;
        File.WriteAllText(Path.Join(directory, "autonomy-settings.json"), """{"enabled":false}""");
        Assert.That(new JsonAgentWorkspaceSettingsRepository(directory).ServerEnabled, Is.False);

        File.WriteAllText(Path.Join(directory, "autonomy-settings.json"), "{}");
        Assert.That(new JsonAgentWorkspaceSettingsRepository(directory).ServerEnabled, Is.False);

        File.WriteAllText(Path.Join(directory, "autonomy-settings.json"), """{"enabled":1}""");
        Assert.That(new JsonAgentWorkspaceSettingsRepository(directory).ServerEnabled, Is.False);
    }

    [Test]
    public void Load_listsPersistedWorkspacesAndReturnsEmptyForUnknownIds()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-settings-load").FullName;
        var first = new JsonAgentWorkspaceSettingsRepository(directory);
        first.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws-1") with { HostConnectionAuthorized = true });
        first.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws-2"));

        var loaded = new JsonAgentWorkspaceSettingsRepository(directory);

        Assert.That(loaded.List().Select(item => item.WorkspaceId), Is.EquivalentTo(new[] { "ws-1", "ws-2" }));
        Assert.That(loaded.Get("missing").HostConnectionAuthorized, Is.False);
    }

    [Test]
    public void Constructor_throwsWhenAutonomySettingsAreNotJson()
    {
        var directory = Directory.CreateTempSubdirectory("agent-up-autonomy-invalid").FullName;
        File.WriteAllText(Path.Join(directory, "autonomy-settings.json"), "{not-json");

        Assert.That(
            () => new JsonAgentWorkspaceSettingsRepository(directory),
            Throws.InstanceOf<System.Text.Json.JsonException>());
    }

    [Test]
    public void Save_createsAMissingDataDirectory()
    {
        var directory = Path.Join(Directory.CreateTempSubdirectory("agent-up-settings-nested").FullName, "nested");
        var repository = new JsonAgentWorkspaceSettingsRepository(directory);
        repository.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws-1") with { HostConnectionAuthorized = true });

        Assert.That(File.Exists(Path.Join(directory, "agent-workspace-settings.json")), Is.True);
        Assert.That(new JsonAgentWorkspaceSettingsRepository(directory).Get("ws-1").HostConnectionAuthorized, Is.True);
    }
}
