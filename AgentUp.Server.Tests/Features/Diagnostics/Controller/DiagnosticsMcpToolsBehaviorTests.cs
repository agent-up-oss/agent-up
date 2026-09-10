using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Diagnostics.Controllers;
using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Diagnostics.Services;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Processes.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Shared.Providers;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Diagnostics.Controller;

[TestFixture]
public sealed class DiagnosticsMcpToolsBehaviorTests
{
    [Test]
    public async Task GetWorkspaceDiagnostics_ReturnsSuccessForKnownWorkspace()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(new AgentUp.Server.Features.Workspaces.DTOs.RegisterWorkspaceRequest(
            "Shop", "/repo", "/repo", "main", "abc"));
        var audit = ServerTestComposition.CreateAuditController(registry);
        var health = new AppHealthController(new AppHealthCheckService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            audit,
            NullLogger<AppHealthCheckService>.Instance));
        var diagnostics = new WorkspaceDiagnosticsController(new WorkspaceDiagnosticsService(
            new WorkspaceQueryController(registry),
            new ProcessesController(new NullWorkspaceProcessManager(), new ProcessOutputService(
                new InMemoryOutputRepository(),
                audit,
                NullLogger<ProcessOutputService>.Instance)),
            health,
            audit,
            new ConsoleSecretRedactor()));
        var tools = new DiagnosticsMcpTools(diagnostics);

        var result = await tools.GetWorkspaceDiagnostics(workspace.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("Shop"));
            Assert.That(result.Data, Is.TypeOf<WorkspaceDiagnosticsDto>());
        });
    }

    [Test]
    public async Task GetWorkspaceDiagnostics_ReturnsFailureForMissingWorkspace()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var audit = ServerTestComposition.CreateAuditController(registry);
        var health = new AppHealthController(new AppHealthCheckService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            audit,
            NullLogger<AppHealthCheckService>.Instance));
        var diagnostics = new WorkspaceDiagnosticsController(new WorkspaceDiagnosticsService(
            new WorkspaceQueryController(registry),
            new ProcessesController(new NullWorkspaceProcessManager(), new ProcessOutputService(
                new InMemoryOutputRepository(),
                audit,
                NullLogger<ProcessOutputService>.Instance)),
            health,
            audit,
            new ConsoleSecretRedactor()));
        var tools = new DiagnosticsMcpTools(diagnostics);

        var result = await tools.GetWorkspaceDiagnostics("missing-workspace");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("missing-workspace"));
        });
    }
}
