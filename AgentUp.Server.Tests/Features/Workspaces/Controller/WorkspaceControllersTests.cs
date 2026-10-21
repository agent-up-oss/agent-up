using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Providers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class WorkspaceControllersTests
{
    [Test]
    public async Task Query_controller_registers_and_finds_applications_by_exact_name()
    {
        var controller = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await controller.RegisterAsync(ServerDomain.Workspace()
            .Named("Sample")
            .At("/repo")
            .AtCommit("abc")
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "npm start").Build())
            .Build());

        Assert.That(controller.GetById(workspace.Id), Is.SameAs(workspace));
        Assert.That(controller.HasApplication(workspace.Id, "Web"), Is.True);
        Assert.That(controller.HasApplication(workspace.Id, "web"), Is.False);
    }

    [Test]
    public async Task State_controller_updates_registered_workspace_and_application_state()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .Named("Sample")
            .At("/repo")
            .AtCommit("abc")
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, "npm start").Build())
            .Build());
        var controller = ServerTestComposition.CreateWorkspaceStateController(registry);

        var workspaceUpdated = await controller.UpdateWorkspaceStateAsync(workspace.Id, WorkspaceState.Running);
        var applicationUpdated = await controller.UpdateApplicationStateAsync(
            workspace.Id, "Web", ApplicationState.Running);

        Assert.That(workspaceUpdated, Is.True);
        Assert.That(applicationUpdated, Is.True);
        Assert.That(registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Running));
        Assert.That(registry.GetById(workspace.Id)!.Applications.Single().State, Is.EqualTo(ApplicationState.Running));
    }

    [Test]
    public async Task Start_returnsOkWithFailedApplications_whenTheWorkspaceIsDegraded()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var created = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).Build())
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.ApiName, ServerDomain.ApiCommand).Build())
            .Build());
        var lifecycle = ServerTestComposition.CreateWorkspaceLifecycleService(
            registry, new PartialFailWorkspaceProcessManager());
        var controller = new WorkspacesController(
            registry,
            lifecycle,
            new WorkspaceEventStreamService(
                new WorkspaceEventBus(),
                new AgentUp.Server.Features.Workspaces.Providers.WorkspaceEventFrameProvider()),
            new WorkspaceOverviewService(
                new WorkspaceQueryController(registry),
                ServerTestComposition.CreateProcessesController(new PartialFailWorkspaceProcessManager()),
                new AgentUp.Server.Features.Workspaces.Providers.WorkspaceDiskUsageProvider()));

        var result = await controller.Start(created.Id);

        var ok = result as Microsoft.AspNetCore.Mvc.OkObjectResult;
        var outcome = ok?.Value as WorkspaceStartOutcome;
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.Not.Null);
            Assert.That(outcome!.State, Is.EqualTo(WorkspaceState.Degraded));
            Assert.That(outcome.FailedApplications.Single().Name, Is.EqualTo(ServerDomain.ApiName));
            Assert.That(registry.GetById(created.Id)!.State, Is.EqualTo(WorkspaceState.Degraded));
        });
    }
}
