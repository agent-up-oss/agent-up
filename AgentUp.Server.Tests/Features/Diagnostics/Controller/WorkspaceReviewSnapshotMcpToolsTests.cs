using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Features.Diagnostics.Controllers;
using AgentUp.Server.Features.Diagnostics.Providers;
using AgentUp.Server.Features.Diagnostics.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Shared.Providers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Diagnostics.Controller;

[TestFixture]
public sealed class WorkspaceReviewSnapshotMcpToolsTests
{
    [Test]
    public void ToolContract_DescribesShareableWorkspaceEvidence()
    {
        var description = typeof(WorkspaceReviewSnapshotMcpTools)
            .GetMethod(nameof(WorkspaceReviewSnapshotMcpTools.ExportWorkspaceReviewSnapshot))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single().Description;

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("HTML"));
            Assert.That(description, Does.Contain("allocated ports"));
            Assert.That(description, Does.Contain("browser URL"));
            Assert.That(description, Does.Contain("recent MCP"));
            Assert.That(description, Does.Contain("export_verification"));
            Assert.That(description, Does.Contain("verify guard"));
        });
    }

    [Test]
    public async Task ExportWorkspaceReviewSnapshot_ReturnsHtmlForARegisteredWorkspace()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .Named("Shop")
            .At(ServerDomain.RepositoryPath)
            .AtCommit("abc")
            .Build());
        var tools = Tools(registry);

        var result = await tools.ExportWorkspaceReviewSnapshot(workspace.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("HTML"));
            Assert.That(result.Data, Is.TypeOf<string>());
            Assert.That(result.Data as string, Does.Contain("Shop").And.Contain("<!DOCTYPE html>"));
        });
    }

    [Test]
    public async Task ExportWorkspaceReviewSnapshot_ReturnsFailureForMissingWorkspace()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var tools = Tools(registry);

        var result = await tools.ExportWorkspaceReviewSnapshot("missing-workspace");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("missing-workspace"));
        });
    }

    private static WorkspaceReviewSnapshotMcpTools Tools(WorkspaceRegistry registry)
    {
        var audit = ServerTestComposition.CreateAuditController(registry);
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
        var snapshots = new WorkspaceReviewSnapshotController(new WorkspaceReviewSnapshotService(
            new WorkspaceQueryController(registry),
            diagnostics,
            new BrowserSessionController(new BrowserSessionStore(), new HeadlessBrowserSessionAccessor(null)),
            audit,
            new ReviewSnapshotHtmlProvider(new ReviewSnapshotCssProvider())));
        return new WorkspaceReviewSnapshotMcpTools(snapshots);
    }
}
