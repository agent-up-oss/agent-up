using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Providers;
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
        await repository.AppendAsync(Event("workspace-a", "feature"), CancellationToken.None);
        await repository.AppendAsync(Event("workspace-b", "other"), CancellationToken.None);

        var result = await repository.QueryAsync(
            new AuditEventQuery("workspace-a", null, null, "main", null, null, null, null, null, null, 10),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Select(evt => evt.WorkspaceId), Is.EqualTo(["workspace-a"]));
            Assert.That(result.Select(evt => evt.Branch), Is.EqualTo(["main"]));
        });
    }

    [Test]
    public async Task EventRepository_FiltersByScope()
    {
        var repository = new FileAuditEventRepository(_dir);
        await repository.AppendAsync(Event("workspace-a", "main", scope: null), CancellationToken.None);
        await repository.AppendAsync(Event("workspace-a", "main", scope: "host-server"), CancellationToken.None);
        await repository.AppendAsync(Event("workspace-a", "main", scope: "application"), CancellationToken.None);

        var hostEvents = await repository.QueryAsync(
            new AuditEventQuery(null, null, null, null, null, null, null, null, null, null, 10, "host-server"),
            CancellationToken.None);

        Assert.That(hostEvents.Select(evt => evt.Scope), Is.EqualTo(["host-server"]));
    }

    [TestCase("application")]
    [TestCase("applicationName")]
    [TestCase("appName")]
    public async Task EventRepository_FiltersAllSupportedApplicationContextKeys(string key)
    {
        var repository = new FileAuditEventRepository(_dir);
        var evt = Event("workspace-a", "main") with
        {
            Details = new Dictionary<string, string> { [key] = "Web" }
        };
        await repository.AppendAsync(evt, CancellationToken.None);

        var result = await repository.QueryAsync(
            new AuditEventQuery("workspace-a", null, null, null, null, null, null, null, null, null, 10, Application: "web"),
            CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task QueryAsync_ReturnsNewestMatchesWithoutScanningEntireHistory()
    {
        var repository = new FileAuditEventRepository(_dir);
        var timestamp = DateTimeOffset.Parse("2026-08-22T12:00:00Z");
        foreach (var eventId in new[] { "event-005", "event-100", "event-003", "event-050", "event-999" })
            await repository.AppendAsync(CreateEvent(eventId, timestamp), CancellationToken.None);

        var page = await repository.QueryAsync(
            new AuditEventQuery(
                "ws-1",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                3,
                Application: "web"),
            CancellationToken.None);

        Assert.That(page.Select(evt => evt.EventId), Is.EqualTo(["event-999", "event-100", "event-050"]));
    }

    [Test]
    public async Task QueryAsync_CompositeCursorPagesThroughEqualTimestamps()
    {
        var repository = new FileAuditEventRepository(_dir);
        var timestamp = DateTimeOffset.Parse("2026-08-22T12:00:00Z");
        await repository.AppendAsync(CreateEvent("event-b", timestamp), CancellationToken.None);
        await repository.AppendAsync(CreateEvent("event-a", timestamp), CancellationToken.None);

        var first = await repository.QueryAsync(
            new AuditEventQuery("ws-1", null, null, null, null, null, null, null, null, null, 1, Application: "web"),
            CancellationToken.None);
        var second = await repository.QueryAsync(
            new AuditEventQuery(
                "ws-1",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                1,
                Application: "web",
                Before: first[0].Timestamp,
                BeforeEventId: first[0].EventId),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(first.Single().EventId, Is.EqualTo("event-b"));
            Assert.That(second.Single().EventId, Is.EqualTo("event-a"));
        });
    }

    [Test]
    public async Task ReverseReader_ReturnsLinesFromNewestToOldest()
    {
        var path = Path.Join(_dir, "sample.jsonl");
        await File.WriteAllTextAsync(path, "first\nsecond\nthird\n");

        var lines = new List<string>();
        await foreach (var line in AuditEventLogReverseReader.ReadLinesReverseAsync(path, CancellationToken.None))
            lines.Add(line);

        Assert.That(lines, Is.EqualTo(["third", "second", "first"]));
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
            var value = loaded.GetValueOrDefault();
            Assert.That(value.Metadata.ArtifactId, Is.EqualTo(saved.ArtifactId));
            Assert.That(value.Bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
        });
    }

    private static AuditEvent Event(string workspaceId, string branch, string? scope = null) =>
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
            [],
            scope);

    private static AuditEvent CreateEvent(string eventId, DateTimeOffset timestamp)
        => new(eventId, timestamp, "frontend", "web", "load", "success", "ws-1",
            null, null, null, null, null, null,
            new Dictionary<string, string> { ["application"] = "web" }, []);

    [Test]
    public async Task QueryAsync_SkipsOtherApplicationsWhenScanningNewestEventsFirst()
    {
        var repository = new FileAuditEventRepository(_dir);
        var timestamp = DateTimeOffset.Parse("2026-08-22T12:00:00Z");
        for (var index = 0; index < 200; index++)
            await repository.AppendAsync(CreateIndexedEvent(index, timestamp, "noisy"), CancellationToken.None);
        for (var index = 0; index < 5; index++)
            await repository.AppendAsync(CreateIndexedEvent(index, timestamp, "quiet"), CancellationToken.None);

        var page = await repository.QueryAsync(
            new AuditEventQuery(
                "ws-1",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                3,
                Application: "quiet",
                Kinds: ["frontend"]),
            CancellationToken.None);

        Assert.That(page.Select(evt => evt.EventId), Is.EqualTo(["event-4", "event-3", "event-2"]));
    }

    [Test]
    public async Task AppendAsync_WritesApplicationIndexWithGlobalOffset()
    {
        var repository = new FileAuditEventRepository(_dir);
        var timestamp = DateTimeOffset.Parse("2026-08-22T12:00:00Z");
        await repository.AppendAsync(CreateIndexedEvent(1, timestamp, "web"), CancellationToken.None);

        var auditDir = Path.Join(_dir, "audit");
        var indexFile = Path.Join(
            auditDir,
            "indexes",
            "ws-1",
            AuditApplicationIndexPathsProvider.EncodeApplicationKey("web"),
            "2026-08-22.idx.jsonl");
        var globalFile = Path.Join(auditDir, "events-2026-08-22.jsonl");

        Assert.That(File.Exists(indexFile), Is.True);
        Assert.That(AuditEventIndexLineCodec.TryParse(File.ReadAllText(indexFile).Trim(), out var indexEntry), Is.True);
        Assert.That(indexEntry!.Kind, Is.EqualTo("frontend"));

        var indexedLine = await AuditEventOffsetLineReader.ReadLineAsync(
            globalFile,
            indexEntry.Offset,
            indexEntry.Length,
            CancellationToken.None);

        Assert.That(indexedLine, Does.Contain("\"EventId\":\"event-1\""));
    }

    [Test]
    public async Task QueryAsync_BuildsLegacyApplicationIndexOnFirstAccess()
    {
        var repository = new FileAuditEventRepository(_dir);
        var timestamp = DateTimeOffset.Parse("2026-08-22T12:00:00Z");
        var globalFile = Path.Join(_dir, "audit", "events-2026-08-22.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(globalFile)!);
        await File.AppendAllTextAsync(
            globalFile,
            $$"""{"EventId":"legacy-1","Timestamp":"{{timestamp:O}}","Kind":"frontend","Source":"web","Action":"load","Outcome":"success","WorkspaceId":"ws-1","RepositoryPath":null,"WorktreePath":null,"WorkdirId":null,"Branch":null,"Commit":null,"Dirty":null,"Details":{"application":"web","message":"legacy"},"ArtifactIds":[],"Scope":null}""" + Environment.NewLine);

        var page = await repository.QueryAsync(
            new AuditEventQuery("ws-1", null, null, null, null, null, null, null, null, null, 1, Application: "web"),
            CancellationToken.None);

        var auditDir = Path.Join(_dir, "audit");
        var indexFile = Path.Join(
            auditDir,
            "indexes",
            "ws-1",
            AuditApplicationIndexPathsProvider.EncodeApplicationKey("web"),
            "2026-08-22.idx.jsonl");

        Assert.Multiple(() =>
        {
            Assert.That(page.Single().EventId, Is.EqualTo("legacy-1"));
            Assert.That(File.Exists(indexFile), Is.True);
        });
    }

    private static AuditEvent CreateIndexedEvent(int index, DateTimeOffset timestamp, string application)
        => CreateEvent($"event-{index}", timestamp) with
        {
            Details = new Dictionary<string, string> { ["application"] = application }
        };

    private static AuditEvent CreateIndexedEvent(int index, DateTimeOffset timestamp)
        => CreateIndexedEvent(index, timestamp, "web");
}
