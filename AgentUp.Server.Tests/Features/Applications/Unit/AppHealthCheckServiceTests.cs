using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Applications.Unit;

[TestFixture]
public sealed class AppHealthCheckServiceTests
{
    [Test]
    public async Task StartForWorkspace_doesNotOverwriteStartingWithChecking()
    {
        var (service, workspace) = await CreateAsync(ApplicationState.Starting);

        service.StartForWorkspace(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Starting));
            Assert.That(service.GetPortHealth(workspace.Id, ServerDomain.WebName)!.Single().HealthState,
                Is.EqualTo("Checking"));
        });
        service.Dispose();
    }

    [Test]
    public async Task StartForWorkspace_movesARunningApplicationToChecking()
    {
        var (service, workspace) = await CreateAsync(ApplicationState.Running);

        service.StartForWorkspace(workspace);

        Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Checking));
        service.Dispose();
    }

    [Test]
    public async Task StartForWorkspace_leavesAFailedApplicationFailed()
    {
        var (service, workspace) = await CreateAsync(ApplicationState.Failed);

        service.StartForWorkspace(workspace);

        Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Failed));
        service.Dispose();
    }

    [Test]
    public async Task StartForWorkspace_movesAnUnhealthyApplicationToChecking()
    {
        var (service, workspace) = await CreateAsync(ApplicationState.Unhealthy);

        service.StartForWorkspace(workspace);

        Assert.That(workspace.Applications.Single().State, Is.EqualTo(ApplicationState.Checking));
        service.Dispose();
    }

    private static async Task<(AppHealthCheckService Service, Workspace Workspace)> CreateAsync(
        ApplicationState state)
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand)
                .WithPort(new PortDeclarationBuilder().WithHealthCheck("/health"))
                .Build())
            .Build());
        workspace.Applications.Single().AllocatedPorts =
            [new PortMapping("WEB_PORT", 3000, 10300, "http", "/health")];
        workspace.Applications.Single().State = state;
        var service = new AppHealthCheckService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            ServerTestComposition.CreateAuditController(registry),
            NullLogger<AppHealthCheckService>.Instance);
        return (service, workspace);
    }
}
