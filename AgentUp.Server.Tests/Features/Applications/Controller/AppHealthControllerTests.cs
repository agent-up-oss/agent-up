using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Applications.Controller;

[TestFixture]
public sealed class AppHealthControllerTests
{
    [Test]
    public void Unknown_workspace_has_no_aggregate_or_port_health()
    {
        var controller = CreateController();

        Assert.That(controller.GetWorkspaceHealth("missing"), Is.Null);
        Assert.That(controller.GetPortHealth("missing", "web"), Is.Null);
    }

    [Test]
    public void Starting_and_stopping_a_workspace_without_health_checks_is_safe()
    {
        var controller = CreateController();
        var workspace = new AgentUp.Server.Features.Workspaces.DTOs.Workspace
        {
            Id = "one",
            DisplayName = "One",
            RepositoryPath = "/repo",
            WorktreePath = "/repo",
            Branch = "main",
            Commit = "abc",
            Applications = []
        };

        Assert.DoesNotThrow(() => controller.StartForWorkspace(workspace));
        Assert.DoesNotThrow(() => controller.StopForWorkspace(workspace.Id));
    }

    private static AppHealthController CreateController()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var query = new WorkspaceQueryController(registry);
        var state = new WorkspaceStateController(registry, new WorkspaceEventBus());
        return new AppHealthController(new AppHealthCheckService(
            query, state, ServerTestComposition.CreateAuditController(registry),
            NullLogger<AppHealthCheckService>.Instance));
    }
}
