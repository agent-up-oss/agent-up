using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class AgentMcpSessionControllerTests
{
    [Test]
    public void Resolve_namesTheWorkspaceAndItsMembersCurrentPermissions()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var sessions = new AgentMcpSessionService();
        var controller = Controller(sessions, settings);
        var token = sessions.Issue("reviewer-1");
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty("reviewer-1") with { Member = TeamMemberKind.DevReviewer });

        var resolved = controller.Resolve(token);

        Assert.Multiple(() =>
        {
            Assert.That(resolved!.WorkspaceId, Is.EqualTo("reviewer-1"));
            Assert.That(resolved.Permissions, Does.Contain("git.read").And.Contain("agent.prompt"));
            // A dev reviewer starts the revision to see it run, but never writes or drives the browser.
            Assert.That(resolved.Permissions, Does.Contain("workspace.start"));
            Assert.That(resolved.Permissions, Does.Not.Contain("git.write").And.Not.Contain("browser.control"));
        });
    }

    [Test]
    public void Resolve_returnsNothingForACredentialThatIsNotLive()
    {
        var controller = Controller(new AgentMcpSessionService(), new InMemoryAgentWorkspaceSettingsRepository());

        Assert.That(controller.Resolve("not-issued"), Is.Null);
    }

    private static AgentMcpSessionController Controller(AgentMcpSessionService sessions, InMemoryAgentWorkspaceSettingsRepository settings) =>
        new(sessions, new TeamMemberService(settings, new WorkspaceQueryController(ServerTestComposition.CreateRegistry())));
}
