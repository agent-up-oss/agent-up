using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Repositories;

namespace AgentUp.Server.Tests.Features.Audit.Repository;

[TestFixture]
public sealed class FileAuditRepositoryTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Join(Path.GetTempPath(), "agentup-audit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Test]
    public async Task EventRepository_AppendsAndFiltersEvents()
    {
        var repository = new FileAuditEventRepository(_dir);
        await repository.AppendAsync(Event("workspace-a", "main"), CancellationToken.None);
        await repository.AppendAsync(Event("workspace-b", "other"), CancellationToken.None);

        var result = await repository.QueryAsync(
            new AuditEventQuery("workspace-a", null, null, "main", null, null, null, null, null, null, 10),
            CancellationToken.None);

        Assert.That(result.Select(evt => evt.WorkspaceId), Is.EqualTo(["workspace-a"]));
    }

    [Test]
    public async Task ArtifactRepository_SavesAndLoadsBytes()
    {
        var repository = new FileAuditArtifactRepository(_dir);

        var saved = await repository.SaveAsync("evt", "browser-screenshot", "image/png", [1, 2, 3], CancellationToken.None);
        var loaded = await repository.LoadAsync(saved.ArtifactId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.Value.Metadata.ArtifactId, Is.EqualTo(saved.ArtifactId));
            Assert.That(loaded.Value.Bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
        });
    }

    private static AuditEvent Event(string workspaceId, string branch) =>
        new(
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow,
            "browser",
            "mcp",
            "browser_click",
            "success",
            workspaceId,
            "/repo",
            "/repo/worktree",
            "workdir",
            branch,
            "abc123",
            false,
            new Dictionary<string, string>(),
            []);
}
