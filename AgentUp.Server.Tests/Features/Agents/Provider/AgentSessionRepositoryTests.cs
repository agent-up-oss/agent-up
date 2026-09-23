using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSessionRepositoryTests
{
    private readonly List<string> _directories = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var directory in _directories.Where(Directory.Exists))
            Directory.Delete(directory, true);
        _directories.Clear();
    }

    [Test]
    public void Upsert_partitionsByWorkspaceAndOrdersByLastUse()
    {
        var (_, repository) = Repository();
        var older = Session("workspace-a", "older", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var newer = Session("workspace-a", "newer", DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        repository.Upsert(older);
        repository.Upsert(Session("workspace-b", "other", newer.LastUsedAt));
        repository.Upsert(newer);

        Assert.Multiple(() =>
        {
            Assert.That(repository.List("workspace-a").Select(item => item.SessionId), Is.EqualTo(new[] { "newer", "older" }));
            Assert.That(repository.List("workspace-b").Single().SessionId, Is.EqualTo("other"));
            Assert.That(repository.Find("workspace-a", "other"), Is.Null);
        });
    }

    [Test]
    public void Upsert_replacesMatchingSessionAndPersistsAllFields()
    {
        var (directory, repository) = Repository();
        repository.Upsert(Session("workspace", "session", DateTimeOffset.UnixEpoch));
        var replacement = new PersistedAgentSession("workspace", "session", "claude", "Generated title", "feature/sessions", DateTimeOffset.UtcNow);

        repository.Upsert(replacement);

        Assert.That(repository.List("workspace"), Is.EqualTo(new[] { replacement }));
        Assert.That(JsonDocument.Parse(File.ReadAllText(Path.Join(directory, "agent-sessions.json"))).RootElement.GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public void Rekey_removesBothOldIdAndAnExistingNewId()
    {
        var (_, repository) = Repository();
        repository.Upsert(Session("workspace", "old", DateTimeOffset.UnixEpoch));
        repository.Upsert(Session("workspace", "new", DateTimeOffset.UnixEpoch));
        repository.Upsert(Session("other", "old", DateTimeOffset.UnixEpoch));
        var replacement = Session("workspace", "new", DateTimeOffset.UtcNow);

        repository.Rekey(replacement, "old");

        Assert.Multiple(() =>
        {
            Assert.That(repository.List("workspace"), Is.EqualTo(new[] { replacement }));
            Assert.That(repository.List("other").Single().SessionId, Is.EqualTo("old"));
        });
    }

    [Test]
    public void RemoveWorkspace_usesValidReplacementAndPreservesOtherWorkspaces()
    {
        var (directory, repository) = Repository();
        repository.Upsert(Session("remove", "one", DateTimeOffset.UnixEpoch));
        var keep = Session("keep", "two", DateTimeOffset.UtcNow);
        repository.Upsert(keep);

        repository.RemoveWorkspace("remove");

        Assert.Multiple(() =>
        {
            Assert.That(repository.List("remove"), Is.Empty);
            Assert.That(repository.List("keep"), Is.EqualTo(new[] { keep }));
            Assert.That(File.Exists(Path.Join(directory, "agent-sessions.json.tmp")), Is.False);
        });
    }

    [Test]
    public void EmptyAndInvalidFilesReturnAnEmptyList()
    {
        var (directory, repository) = Repository();

        Assert.That(repository.List("workspace"), Is.Empty);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, "agent-sessions.json"), "not json");

        Assert.Multiple(() =>
        {
            Assert.That(repository.List("workspace"), Is.Empty);
            Assert.That(() => repository.RemoveWorkspace("workspace"), Throws.Nothing);
            Assert.That(repository.List("workspace"), Is.Empty);
        });
    }

    /// <summary>A repository over a directory of its own, removed when the test ends.</summary>
    private (string Directory, AgentSessionRepository Repository) Repository()
    {
        var directory = Path.Join(Path.GetTempPath(), $"agent-up-agent-sessions-{Guid.NewGuid():N}");
        _directories.Add(directory);
        return (directory, new AgentSessionRepository(directory));
    }

    private static PersistedAgentSession Session(string workspace, string session, DateTimeOffset lastUsed) =>
        new(workspace, session, "codex", $"Description {session}", "main", lastUsed);
}
