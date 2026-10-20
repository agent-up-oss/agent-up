using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Diagnostics.Controllers;
using AgentUp.Server.Features.Diagnostics.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Shared.Providers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Diagnostics.Unit;

[TestFixture]
public sealed class WorkspaceReviewSnapshotServiceTests
{
    [Test]
    public async Task GetAsync_CapturesIdentityPortsAndRecentActions()
    {
        var (registry, audit, workspace) = await CreateAsync();
        workspace.Applications.Single().AllocatedPorts =
            [new AgentUp.Server.Features.Ports.DTOs.PortMapping("WEB_PORT", 3000, 4010)];
        await audit.RecordAsync(ServerDomain.AuditRecord()
            .OfKind("mcp")
            .From("orchestration")
            .Doing("start_workspace")
            .Outcome("success")
            .ForWorkspace(workspace.Id)
            .Build(), CancellationToken.None);

        var snapshot = await Snapshot(registry, audit).GetAsync(workspace.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot!.DisplayName, Is.EqualTo("Shop"));
            Assert.That(snapshot.Commit, Is.EqualTo("abc"));
            Assert.That(snapshot.Applications.Single().Ports.Single().AllocatedPort, Is.EqualTo(4010));
            Assert.That(snapshot.RecentActions.Select(action => action.Action), Does.Contain("start_workspace"));
            Assert.That(snapshot.Verification, Is.Null);
        });
    }

    [Test]
    public async Task GetAsync_ReturnsNullForUnknownWorkspace()
    {
        var (registry, audit, _) = await CreateAsync();
        Assert.That(await Snapshot(registry, audit).GetAsync("missing", CancellationToken.None), Is.Null);
    }

    private static async Task<(WorkspaceRegistry Registry, AgentUp.Server.Features.Audit.Controllers.AuditController Audit, Workspace Workspace)> CreateAsync()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .Named("Shop")
            .At("/repo")
            .AtCommit("abc")
            .WithApplication(new ApplicationDefinitionBuilder("web", "npm start").At(".").Build())
            .Build());
        var audit = ServerTestComposition.CreateAuditController(registry);
        return (registry, audit, workspace);
    }

    private static WorkspaceReviewSnapshotService Snapshot(
        WorkspaceRegistry registry,
        AgentUp.Server.Features.Audit.Controllers.AuditController audit)
    {
        var health = new AppHealthController(new AppHealthCheckService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateWorkspaceStateController(registry),
            audit,
            NullLogger<AppHealthCheckService>.Instance));
        var diagnostics = new WorkspaceDiagnosticsController(new WorkspaceDiagnosticsService(
            new WorkspaceQueryController(registry),
            ServerTestComposition.CreateProcessesController(new NullWorkspaceProcessManager()),
            health,
            audit,
            new ConsoleSecretRedactor()));
        return new WorkspaceReviewSnapshotService(
            new WorkspaceQueryController(registry),
            diagnostics,
            new BrowserSessionController(new BrowserSessionStore(), new HeadlessBrowserSessionAccessor(null)),
            audit);
    }
}
