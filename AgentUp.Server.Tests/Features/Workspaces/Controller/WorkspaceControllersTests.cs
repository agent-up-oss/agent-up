using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class WorkspaceControllersTests
{
    [Test]
    public async Task Query_controller_registers_and_finds_applications_by_exact_name()
    {
        var controller = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await controller.RegisterAsync(new RegisterWorkspaceRequest(
            "Sample", "/repo", "/repo", "main", "abc")
        {
            Applications = [new ApplicationDefinition("Web", "npm start", null)]
        });

        Assert.That(controller.GetById(workspace.Id), Is.SameAs(workspace));
        Assert.That(controller.HasApplication(workspace.Id, "Web"), Is.True);
        Assert.That(controller.HasApplication(workspace.Id, "web"), Is.False);
    }

    [Test]
    public async Task State_controller_updates_registered_workspace_and_application_state()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest(
            "Sample", "/repo", "/repo", "main", "abc")
        {
            Applications = [new ApplicationDefinition("Web", "npm start", null)]
        });
        var controller = ServerTestComposition.CreateWorkspaceStateController(registry);

        var workspaceUpdated = await controller.UpdateWorkspaceStateAsync(workspace.Id, WorkspaceState.Running);
        var applicationUpdated = await controller.UpdateApplicationStateAsync(
            workspace.Id, "Web", ApplicationState.Running);

        Assert.That(workspaceUpdated, Is.True);
        Assert.That(applicationUpdated, Is.True);
        Assert.That(registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Running));
        Assert.That(registry.GetById(workspace.Id)!.Applications.Single().State, Is.EqualTo(ApplicationState.Running));
    }
}
