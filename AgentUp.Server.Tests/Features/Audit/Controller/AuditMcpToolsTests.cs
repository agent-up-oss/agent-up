using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Audit.Controller;

[TestFixture]
public sealed class AuditMcpToolsTests
{
    [Test]
    public async Task Query_ReturnsMatchingAuditEvents()
    {
        var events = new InMemoryAuditEventRepository();
        var controller = ServerTestComposition.CreateAuditController(events: events);
        var tools = new AuditMcpTools(controller);
        await controller.RecordAsync(
            new AuditRecordRequest("browser", "mcp", "browser_click", "success", "workspace"),
            CancellationToken.None);
        await controller.RecordAsync(
            new AuditRecordRequest("workspace", "server", "workspace_state_changed", "success", "other"),
            CancellationToken.None);

        var result = await tools.Query(workspaceId: "workspace", kind: "browser");
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
}
