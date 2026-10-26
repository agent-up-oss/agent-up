using AgentUp.Browser.Streaming;
using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Audit.Models;
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

        var html = await Snapshot(registry, audit).GetAsync(workspace.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Shop"));
            Assert.That(html, Does.Contain("abc"));
            Assert.That(html, Does.Contain("4010"));
            Assert.That(html, Does.Contain("WEB_PORT"));
            Assert.That(html, Does.Contain("start_workspace"));
            Assert.That(html, Does.Contain("No screenshots."));
            Assert.That(html, Does.Contain("Verification could not run"));
        });
    }

    [Test]
    public async Task GetAsync_EmbedsScreenshotImageBytes()
    {
        var (registry, audit, workspace) = await CreateAsync();
        await audit.RecordAsync(ServerDomain.AuditRecord()
            .OfKind("browser")
            .From("mcp")
            .Doing("browser_navigate")
            .Outcome("success")
            .ForWorkspace(workspace.Id)
            .Build(), CancellationToken.None);
        await audit.RecordScreenshotAsync(
            new AuditScreenshot(
                workspace.Id,
                "http://127.0.0.1:4010/",
                "image/png",
                Convert.ToBase64String([1, 2, 3]),
                1280,
                720),
            CancellationToken.None);

        var html = await Snapshot(registry, audit).GetAsync(workspace.Id, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("data:image/png;base64,AQID"));
            Assert.That(html, Does.Contain("http://127.0.0.1:4010/"));
            Assert.That(html, Does.Contain("<h2>Screenshots</h2>"));
        });
    }

    [Test]
    public async Task GetAsync_KeepsScreenshotRefsWhenImageBytesAreMissing()
    {
        var (registry, audit, workspace) = await CreateAsync();
        await audit.RecordAsync(ServerDomain.AuditRecord()
            .OfKind("browser")
            .From("mcp")
            .Doing("browser_screenshot")
            .Outcome("success")
            .ForWorkspace(workspace.Id)
            .WithArtifacts("abc123")
            .Build(), CancellationToken.None);

        var html = await Snapshot(registry, audit).GetAsync(workspace.Id, CancellationToken.None);

        Assert.That(html, Does.Contain("abc123"));
        Assert.That(html, Does.Contain("no embedded image bytes"));
    }

    [Test]
    public async Task GetAsync_CapsScreenshotsAtTen()
    {
        var (registry, audit, workspace) = await CreateAsync();
        for (var index = 0; index < 11; index++)
        {
            await audit.RecordAsync(ServerDomain.AuditRecord()
                .OfKind("browser")
                .From("mcp")
                .Doing("browser_screenshot")
                .Outcome("success")
                .ForWorkspace(workspace.Id)
                .WithArtifacts($"shot{index:D2}")
                .WithDetail("url", $"http://127.0.0.1/{index}")
                .Build(), CancellationToken.None);
        }

        var html = await Snapshot(registry, audit).GetAsync(workspace.Id, CancellationToken.None);
        var included = Enumerable.Range(0, 11).Count(index => html!.Contains($"shot{index:D2}", StringComparison.Ordinal));

        Assert.That(included, Is.EqualTo(10));
    }

    [Test]
    public async Task GetAsync_ReturnsNullForUnknownWorkspace()
    {
        var (registry, audit, _) = await CreateAsync();
        Assert.That(await Snapshot(registry, audit).GetAsync("missing", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task GetAsync_LabelsADockerApplicationAsDocker()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .Named("Shop")
            .At("/repo")
            .AtCommit("abc")
            .WithService(new DockerServiceDefinition("Database", "postgres:16"))
            .Build());
        var audit = ServerTestComposition.CreateAuditController(registry);

        var html = await Snapshot(registry, audit).GetAsync(workspace.Id, CancellationToken.None);

        Assert.That(html, Does.Contain("Database"));
        Assert.That(html, Does.Contain("Docker"));
    }

    private static async Task<(WorkspaceRegistry Registry, AgentUp.Server.Features.Audit.Controllers.AuditController Audit, AgentUp.Server.Features.Workspaces.DTOs.Workspace Workspace)> CreateAsync()
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
            audit,
            new ReviewSnapshotHtmlProvider(new ReviewSnapshotCssProvider()));
    }
}
