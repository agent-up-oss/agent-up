using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class TaskQueueServiceTests
{
    [Test]
    public void Enqueue_keepsTheOrderThingsWerePickedIn()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.Enqueue(Task("2", 2));

        Assert.That(queue.List().Select(task => task.Number), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void Enqueue_pickingTheSameIssueTwiceChangesNothing()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.Enqueue(Task("1", 1));

        Assert.That(queue.List(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Enqueue_treatsTheSameIssueNumberOnAnotherRepositoryAsItsOwnTask()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.Enqueue(Task("1", 1) with { Repository = "other" });

        Assert.That(queue.List(), Has.Count.EqualTo(2));
    }

    [Test]
    public void TryClaim_givesOneIssueToExactlyOneWorkspace()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));

        var first = queue.TryClaim("alice", "acme", "widgets", "1");
        var second = queue.TryClaim("bob", "acme", "widgets", "1");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(queue.List().Single().HeldByWorkspaceId, Is.EqualTo("alice"));
        });
    }

    [Test]
    public void TryClaim_underContentionLetsOnlyOneWorkspaceThrough()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));

        var winners = Enumerable.Range(0, 32)
            .AsParallel()
            .Select(index => queue.TryClaim($"ws{index}", "acme", "widgets", "1"))
            .Count(claimed => claimed);

        Assert.That(winners, Is.EqualTo(1));
    }

    [Test]
    public void TryClaim_isStickyForTheWorkspaceAlreadyCarryingTheTask()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.TryClaim("alice", "acme", "widgets", "1");

        Assert.That(queue.TryClaim("alice", "acme", "widgets", "1"), Is.True);
    }

    [Test]
    public void TryClaim_refusesATaskPointedAtAnotherWorkspace()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1) with { TargetWorkspaceId = "alice" });

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryClaim("bob", "acme", "widgets", "1"), Is.False);
            Assert.That(queue.TryClaim("alice", "acme", "widgets", "1"), Is.True);
        });
    }

    [Test]
    public void TryClaim_refusesATaskFromAnotherRepository()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));

        Assert.That(queue.TryClaim("alice", "acme", "other", "1"), Is.False);
    }

    [Test]
    public void Complete_retiresTheTaskForItsHolderOnly()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.TryClaim("alice", "acme", "widgets", "1");

        Assert.Multiple(() =>
        {
            Assert.That(queue.Complete("bob", "1"), Is.False);
            Assert.That(queue.Complete("alice", "1"), Is.True);
            Assert.That(queue.List(), Is.Empty);
        });
    }

    [Test]
    public void Remove_leavesATaskSomebodyIsCarrying()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.TryClaim("alice", "acme", "widgets", "1");

        Assert.Multiple(() =>
        {
            Assert.That(queue.Remove("acme", "widgets", "1"), Is.False);
            Assert.That(queue.Remove("acme", "widgets", "1", evenWhenHeld: true), Is.True);
        });
    }

    [Test]
    public void Release_handsAHeldTaskBackToTheQueue()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.TryClaim("alice", "acme", "widgets", "1");

        queue.Release("acme", "widgets", "1");

        Assert.Multiple(() =>
        {
            Assert.That(queue.Held("alice"), Is.Null);
            Assert.That(queue.TryClaim("bob", "acme", "widgets", "1"), Is.True);
        });
    }

    [Test]
    public void Move_reordersTheQueueAndClampsToTheEnds()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.Enqueue(Task("2", 2));
        queue.Enqueue(Task("3", 3));

        queue.Move("acme", "widgets", "3", 0);
        queue.Move("acme", "widgets", "1", 99);

        Assert.That(queue.List().Select(task => task.Number), Is.EqualTo(new[] { 3, 2, 1 }));
    }

    [Test]
    public void Target_pointsATaskAtAWorkspaceAndBackAtAnyone()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));

        queue.Target("acme", "widgets", "1", "alice");
        var pointed = queue.List().Single().TargetWorkspaceId;
        queue.Target("acme", "widgets", "1", null);

        Assert.Multiple(() =>
        {
            Assert.That(pointed, Is.EqualTo("alice"));
            Assert.That(queue.List().Single().TargetWorkspaceId, Is.Null);
        });
    }

    [Test]
    public void Held_survivesARestartSoTheSameWorkspacePicksTheTaskBackUp()
    {
        var store = new MemoryTaskQueueStore();
        var queue = new TaskQueueService(store);
        queue.Enqueue(Task("1", 1));
        queue.TryClaim("alice", "acme", "widgets", "1");

        var restarted = new TaskQueueService(store);

        Assert.Multiple(() =>
        {
            Assert.That(restarted.Held("alice")?.Id, Is.EqualTo("1"));
            Assert.That(restarted.TryClaim("bob", "acme", "widgets", "1"), Is.False);
        });
    }

    private static TaskQueueService Queue() => new(new MemoryTaskQueueStore());

    private static QueuedTask Task(string id, int number) =>
        new(id, "acme", "widgets", number, $"Task {number}", "", $"https://example.test/{number}", DateTimeOffset.UnixEpoch);

    private sealed class MemoryTaskQueueStore : ITaskQueueStore
    {
        private IReadOnlyList<QueuedTask> _tasks = [];

        public IReadOnlyList<QueuedTask> Read() => _tasks;

        public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
    }
}
