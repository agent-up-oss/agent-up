using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSessionRepositoryTests
{
    private string _directory = null!;
    private AgentSessionRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Join(Path.GetTempPath(), $"agent-up-agent-sessions-{Guid.NewGuid():N}");
        _repository = new AgentSessionRepository(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void Upsert_partitionsByWorkspaceAndOrdersByLastUse()
    {
        var older = Session("workspace-a", "older", DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var newer = Session("workspace-a", "newer", DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        _repository.Upsert(older);
        _repository.Upsert(Session("workspace-b", "other", newer.LastUsedAt));
        _repository.Upsert(newer);

        Assert.Multiple(() =>
        {
            Assert.That(_repository.List("workspace-a").Select(item => item.SessionId), Is.EqualTo(new[] { "newer", "older" }));
            Assert.That(_repository.List("workspace-b").Single().SessionId, Is.EqualTo("other"));
            Assert.That(_repository.Find("workspace-a", "other"), Is.Null);
        });
    }

    [Test]
    public void Upsert_replacesMatchingSessionAndPersistsAllFields()
    {
        _repository.Upsert(Session("workspace", "session", DateTimeOffset.UnixEpoch));
        var replacement = new PersistedAgentSession("workspace", "session", "claude", "Generated title", "feature/sessions", DateTimeOffset.UtcNow);
        _repository.Upsert(replacement);

        Assert.That(_repository.List("workspace"), Is.EqualTo(new[] { replacement }));
        Assert.That(JsonDocument.Parse(File.ReadAllText(Path.Join(_directory, "agent-sessions.json"))).RootElement.GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public void Rekey_removesBothOldIdAndAnExistingNewId()
    {
        _repository.Upsert(Session("workspace", "old", DateTimeOffset.UnixEpoch));
        _repository.Upsert(Session("workspace", "new", DateTimeOffset.UnixEpoch));
        _repository.Upsert(Session("other", "old", DateTimeOffset.UnixEpoch));
        var replacement = Session("workspace", "new", DateTimeOffset.UtcNow);

        _repository.Rekey(replacement, "old");

        Assert.Multiple(() =>
        {
            Assert.That(_repository.List("workspace"), Is.EqualTo(new[] { replacement }));
            Assert.That(_repository.List("other").Single().SessionId, Is.EqualTo("old"));
        });
    }

    [Test]
    public void RemoveWorkspace_usesValidReplacementAndPreservesOtherWorkspaces()
    {
        _repository.Upsert(Session("remove", "one", DateTimeOffset.UnixEpoch));
        var keep = Session("keep", "two", DateTimeOffset.UtcNow);
        _repository.Upsert(keep);

        _repository.RemoveWorkspace("remove");

        Assert.Multiple(() =>
        {
            Assert.That(_repository.List("remove"), Is.Empty);
            Assert.That(_repository.List("keep"), Is.EqualTo(new[] { keep }));
            Assert.That(File.Exists(Path.Join(_directory, "agent-sessions.json.tmp")), Is.False);
        });
    }

    [Test]
    public void EmptyAndInvalidFilesReturnAnEmptyList()
    {
        Assert.That(_repository.List("workspace"), Is.Empty);
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Join(_directory, "agent-sessions.json"), "not json");
        Assert.That(_repository.List("workspace"), Is.Empty);
        _repository.RemoveWorkspace("workspace");
        Assert.That(_repository.List("workspace"), Is.Empty);
    }

    private static PersistedAgentSession Session(string workspace, string session, DateTimeOffset lastUsed) =>
        new(workspace, session, "codex", $"Description {session}", "main", lastUsed);
}
