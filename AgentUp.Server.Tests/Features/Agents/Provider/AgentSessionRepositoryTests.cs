using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSessionRepositoryTests
{
    [Test]
    public void List_isEmptyBeforeTheStoreExists()
    {
        using var store = CreateStore();
        Assert.That(store.Repository.List("workspace"), Is.Empty);
        Assert.That(store.Repository.Find("workspace", "missing"), Is.Null);
    }

    [Test]
    public void UpsertPartitionsByWorkspaceUpdatesMetadataAndOrdersRecentFirst()
    {
        using var store = CreateStore();
        var repository = store.Repository;
        var older = new PersistedAgentSession("one", "session-1", "codex", "Old", "main", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var recent = new PersistedAgentSession("one", "session-2", "claude", "Recent", "feature", DateTimeOffset.Parse("2026-02-01T00:00:00Z"));
        repository.Upsert(older);
        repository.Upsert(new PersistedAgentSession("two", "session-1", "cursor", "Other", "other", DateTimeOffset.UtcNow));
        repository.Upsert(recent);
        repository.Upsert(older with { Description = "Updated" });

        var sessions = repository.List("one");

        Assert.Multiple(() =>
        {
            Assert.That(sessions.Select(item => item.SessionId), Is.EqualTo(new[] { "session-2", "session-1" }));
            Assert.That(sessions[1].Description, Is.EqualTo("Updated"));
            Assert.That(repository.Find("one", "session-1")!.Agent, Is.EqualTo("codex"));
            Assert.That(repository.List("two"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void RemoveWorkspaceLeavesOtherWorkspaceSessionsIntact()
    {
        using var store = CreateStore();
        store.Repository.Upsert(Session("one", "first"));
        store.Repository.Upsert(Session("two", "second"));

        store.Repository.RemoveWorkspace("one");

        Assert.That(store.Repository.List("one"), Is.Empty);
        Assert.That(store.Repository.List("two").Single().SessionId, Is.EqualTo("second"));
    }

    [Test]
    public void CorruptStoreIsTreatedAsEmptyAndCanBeReplaced()
    {
        using var store = CreateStore();
        Directory.CreateDirectory(store.Directory);
        File.WriteAllText(Path.Join(store.Directory, "agent-sessions.json"), "not-json");

        Assert.That(store.Repository.List("one"), Is.Empty);
        store.Repository.Upsert(Session("one", "recovered"));
        Assert.That(store.Repository.List("one").Single().SessionId, Is.EqualTo("recovered"));
    }

    private static PersistedAgentSession Session(string workspaceId, string sessionId) =>
        new(workspaceId, sessionId, "codex", "Description", "main", DateTimeOffset.UtcNow);

    private static TestStore CreateStore() => new();

    private sealed class TestStore : IDisposable
    {
        public TestStore()
        {
            Directory = Path.Join(Path.GetTempPath(), $"agent-up-session-repository-{Guid.NewGuid():N}");
            Repository = new AgentSessionRepository(Directory);
        }

        public string Directory { get; }
        public AgentSessionRepository Repository { get; }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
        }
    }
}
