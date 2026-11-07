using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class JsonTaskQueueStoreTests
{
    [Test]
    public void Read_isEmptyBeforeAnythingIsPicked()
    {
        using var directory = new TemporaryDirectory();

        Assert.That(new JsonTaskQueueStore(directory.Path).Read(), Is.Empty);
    }

    [Test]
    public void Write_thenRead_keepsTheOrderTheHoldAndTheTarget()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonTaskQueueStore(directory.Path);
        store.Write([
            Task("2", 2) with { HeldByWorkspaceId = "alice" },
            Task("1", 1) with { TargetWorkspaceId = "bob" }
        ]);

        var read = new JsonTaskQueueStore(directory.Path).Read();

        Assert.Multiple(() =>
        {
            Assert.That(read.Select(task => task.Number), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(read[0].HeldByWorkspaceId, Is.EqualTo("alice"));
            Assert.That(read[1].TargetWorkspaceId, Is.EqualTo("bob"));
        });
    }

    [Test]
    public void Read_returnsEmptyRatherThanFailingToStartOnAnUnreadableFile()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(Path.Join(directory.Path, "task-queue.json"), "{ not json");

        Assert.That(new JsonTaskQueueStore(directory.Path).Read(), Is.Empty);
    }

    private static QueuedTask Task(string id, int number) =>
        new(id, "acme", "widgets", number, $"Task {number}", "", $"https://example.test/{number}", DateTimeOffset.UnixEpoch);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Join(System.IO.Path.GetTempPath(), $"agent-up-queue-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
