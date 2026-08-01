using AgentUp.Server.Features.Audit.Controllers;
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

        var result = await tools.Query(workspaceId: "workspace", kind: "browser");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Does.Contain("1"));
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

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Data, Is.Not.Null);
        });
    }
}
