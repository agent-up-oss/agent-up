using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class TeamMemberServiceTests
{
    [Test]
    public async Task Describe_returnsTheKindDefaultSystemPrompt()
    {
        var host = await HostAsync();

        var member = host.Members.Describe(host.WorkspaceId);

        Assert.Multiple(() =>
        {
            Assert.That(member!.SystemPrompt, Is.EqualTo(TeamRoles.AuthorPrompt));
            Assert.That(member.DefaultSystemPrompt, Is.EqualTo(TeamRoles.AuthorPrompt));
            Assert.That(member.CustomizationPrompt, Is.Null);
        });
    }

    [Test]
    public async Task Set_storesASystemPromptOnlyWhenItDiffersFromTheDefault()
    {
        var host = await HostAsync();

        host.Members.Set(host.WorkspaceId, new TeamMemberRequest(TeamMemberKind.Author, "Ship the cards.", "Keep the comma."));
        var customized = host.Members.Describe(host.WorkspaceId);
        host.Members.Set(host.WorkspaceId, new TeamMemberRequest(TeamMemberKind.Author, TeamRoles.AuthorPrompt, "Keep the comma."));
        var restored = host.Members.Describe(host.WorkspaceId);

        Assert.Multiple(() =>
        {
            Assert.That(customized!.SystemPrompt, Is.EqualTo("Ship the cards."));
            Assert.That(customized.CustomizationPrompt, Is.EqualTo("Keep the comma."));
            Assert.That(restored!.SystemPrompt, Is.EqualTo(TeamRoles.AuthorPrompt));
            Assert.That(restored.CustomizationPrompt, Is.EqualTo("Keep the comma."));
        });
    }

    [Test]
    public async Task Set_replacesAModifiedSystemPromptWhenTheKindChangesAndKeepsCustomization()
    {
        var host = await HostAsync();
        host.Members.Set(host.WorkspaceId, new TeamMemberRequest(TeamMemberKind.Author, "Ship the cards.", "Keep the comma."));

        host.Members.Set(host.WorkspaceId, new TeamMemberRequest(TeamMemberKind.DevReviewer));
        var member = host.Members.Describe(host.WorkspaceId);

        Assert.Multiple(() =>
        {
            Assert.That(member!.Kind, Is.EqualTo(TeamMemberKind.DevReviewer));
            Assert.That(member.SystemPrompt, Is.EqualTo(TeamRoles.DevReviewerPrompt));
            Assert.That(member.DefaultSystemPrompt, Is.EqualTo(TeamRoles.DevReviewerPrompt));
            Assert.That(member.CustomizationPrompt, Is.EqualTo("Keep the comma."));
        });
    }

    private static async Task<Host> HostAsync()
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        return new Host(workspace.Id, new TeamMemberService(settings, workspaces));
    }

    private sealed record Host(string WorkspaceId, TeamMemberService Members);
}
