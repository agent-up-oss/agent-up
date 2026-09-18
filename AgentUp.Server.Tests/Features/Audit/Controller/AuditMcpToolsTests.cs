using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Repositories;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Audit.Controller;

[TestFixture]
public sealed class AuditMcpToolsTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Join(Path.GetTempPath(), "agentup-audit-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Test]
    public async Task Query_FiltersByScope()
    {
        var events = new InMemoryAuditEventRepository();
        var controller = ServerTestComposition.CreateAuditController(events: events);
        var tools = new AuditMcpTools(controller);
        await controller.RecordAsync(
            ServerDomain.AuditRecord()
                .OfKind("metrics")
                .From("server")
                .Doing("host_metrics_sample")
                .Outcome("success")
                .ForWorkspace(null)
                .InScope("host-server")
                .Build(),
            CancellationToken.None);
        await controller.RecordAsync(
            ServerDomain.AuditRecord()
                .OfKind("metrics")
                .From("server")
                .Doing("app_metrics_pull")
                .Outcome("success")
                .ForWorkspace("workspace")
                .InScope("application")
                .Build(),
            CancellationToken.None);

        var result = await tools.Query(scope: "host-server", kind: "metrics", compact: false);
        var data = (IReadOnlyList<AuditEvent>)result.Data!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(data, Has.Count.EqualTo(1));
            Assert.That(data.Single().Scope, Is.EqualTo("host-server"));
        });
    }

    [Test]
    public async Task Query_DefaultsToWorkspaceScope_WhenScopeOmitted()
    {
        var events = new InMemoryAuditEventRepository();
        var controller = ServerTestComposition.CreateAuditController(events: events);
        var tools = new AuditMcpTools(controller);
        await controller.RecordAsync(
            ServerDomain.AuditRecord()
                .OfKind("metrics")
                .From("server")
                .Doing("host_metrics_sample")
                .Outcome("success")
                .ForWorkspace(null)
                .InScope("host-server")
                .Build(),
            CancellationToken.None);
        await controller.RecordAsync(
            ServerDomain.AuditRecord()
                .OfKind("workspace")
                .From("server")
                .Doing("workspace_state_changed")
                .Outcome("success")
                .ForWorkspace("workspace")
                .InScope("workspace")
                .Build(),
            CancellationToken.None);

        var result = await tools.Query(kind: null, compact: false);
        var data = (IReadOnlyList<AuditEvent>)result.Data!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(data, Has.Count.EqualTo(1));
            Assert.That(data.Single().Scope, Is.EqualTo("workspace"));
        });
    }

    [Test]
    public async Task Query_ReturnsMatchingAuditEvents()
    {
        var events = new InMemoryAuditEventRepository();
        var controller = ServerTestComposition.CreateAuditController(events: events);
        var tools = new AuditMcpTools(controller);
        await controller.RecordAsync(
            ServerDomain.AuditRecord()
                .OfKind("browser")
                .From("mcp")
                .Doing("browser_click")
                .Outcome("success")
                .ForWorkspace("workspace")
                .Build(),
            CancellationToken.None);
        await controller.RecordAsync(
            ServerDomain.AuditRecord()
                .OfKind("workspace")
                .From("server")
                .Doing("workspace_state_changed")
                .Outcome("success")
                .ForWorkspace("other")
                .Build(),
            CancellationToken.None);

        var result = await tools.Query(workspaceId: "workspace", kind: "browser", compact: false);
        var data = (IReadOnlyList<AuditEvent>)result.Data!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("1"));
            Assert.That(data, Has.Count.EqualTo(1));
            Assert.That(data.Single().WorkspaceId, Is.EqualTo("workspace"));
            Assert.That(data.Single().Kind, Is.EqualTo("browser"));
        });
    }

    [Test]
    public async Task LoadArtifact_ReturnsInlineImage_WhenRequested()
    {
        var artifacts = new InMemoryAuditArtifactRepository();
        var controller = ServerTestComposition.CreateAuditController(artifacts: artifacts);
        var tools = new AuditMcpTools(controller);
        var saved = await artifacts.SaveAsync("evt", "browser-screenshot", "image/png", [1, 2, 3], CancellationToken.None);

        var result = await tools.LoadArtifact(saved.ArtifactId, includeImage: true);
        var data = (AuditArtifactResult)result.Data!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(data.ArtifactId, Is.EqualTo(saved.ArtifactId));
            Assert.That(data.MimeType, Is.EqualTo("image/png"));
            Assert.That(data.ImageBase64, Is.EqualTo(Convert.ToBase64String([1, 2, 3])));
        });
    }

    [TestCase("")]
    [TestCase("../not-hex")]
    public async Task LoadArtifact_ReturnsStructuredFailure_WhenArtifactIdIsInvalid(string artifactId)
    {
        var controller = new AuditController(new AuditService(
            new InMemoryAuditEventRepository(),
            new FileAuditArtifactRepository(_dir),
            new FakeAuditIdentityProvider(),
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()),
            new AuditEventBus()));
        var tools = new AuditMcpTools(controller);

        var result = await tools.LoadArtifact(artifactId);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("was not found"));
        });
    }
}
