using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class AuditServiceTests
{
    [Test]
    public async Task RecordAsync_AddsLiveWorkspaceIdentity()
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(new(
            "App",
            "/repo",
            "/repo/worktree",
            "main",
            "old"));
        var events = new InMemoryAuditEventRepository();
        var identity = new FakeAuditIdentityProvider
        {
            Identity = new("/repo", "/repo/worktree", "wd1", "feature/audit", "live123", true)
        };
        var service = new AuditService(
            events,
            new InMemoryAuditArtifactRepository(),
            identity,
            new WorkspaceQueryController(registry));

        var recorded = await service.RecordAsync(
            new AuditRecordRequest("browser", "mcp", "browser_click", "success", workspace.Id),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(recorded.WorkdirId, Is.EqualTo("wd1"));
            Assert.That(recorded.Branch, Is.EqualTo("feature/audit"));
            Assert.That(recorded.Commit, Is.EqualTo("live123"));
            Assert.That(recorded.Dirty, Is.True);
            Assert.That(events.Events, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task RecordScreenshotAsync_StoresArtifactAndReturnsEvent()
    {
        var service = new AuditService(
            new InMemoryAuditEventRepository(),
            new InMemoryAuditArtifactRepository(),
            new FakeAuditIdentityProvider(),
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()));
        var image = Convert.ToBase64String([1, 2, 3]);

        var result = await service.RecordScreenshotAsync(
            new AuditScreenshot("workspace", "http://localhost:3000", "image/png", image, 1280, 720),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Event.Action, Is.EqualTo("browser_screenshot"));
            Assert.That(result.Event.ArtifactIds, Does.Contain(result.Artifact.ArtifactId));
            Assert.That(result.Artifact.SizeBytes, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task LoadArtifactAsync_ReturnsImageOnlyWhenRequested()
    {
        var artifacts = new InMemoryAuditArtifactRepository();
        var service = new AuditService(
            new InMemoryAuditEventRepository(),
            artifacts,
            new FakeAuditIdentityProvider(),
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()));
        var saved = await artifacts.SaveAsync("evt", "browser-screenshot", "image/png", [1, 2, 3], CancellationToken.None);

        var withoutImage = await service.LoadArtifactAsync(saved.ArtifactId, includeImage: false, CancellationToken.None);
        var withImage = await service.LoadArtifactAsync(saved.ArtifactId, includeImage: true, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(withoutImage!.ImageBase64, Is.Null);
            Assert.That(withImage!.ImageBase64, Is.EqualTo(Convert.ToBase64String([1, 2, 3])));
        });
    }
}
