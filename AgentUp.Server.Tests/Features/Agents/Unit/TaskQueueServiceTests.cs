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

    [Test]
    public void Propose_putsTheTaskInReviewAndCountsRevisions()
    {
        var queue = Held("bob");
        queue.Begin("bob", "acme", "widgets", "1", "main");
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");
        var task = queue.Get("acme", "widgets", "1")!;

        Assert.Multiple(() =>
        {
            Assert.That(task.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(task.Proposal!.Revision, Is.EqualTo(2));
            Assert.That(task.BaseBranch, Is.EqualTo("main"));
        });
    }

    [Test]
    public void Review_sendsTheTaskBackToItsAuthorOnAnyRequestForChanges()
    {
        var queue = InReview();

        var reviewed = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.RequestChanges), ["lucy", "hector"]);

        Assert.That(reviewed!.Stage, Is.EqualTo(TaskStage.ChangesRequested));
    }

    [Test]
    public void Review_approvesOnlyWhenEveryReviewerApprovesTheSameRevision()
    {
        var queue = InReview();

        var one = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve), ["lucy", "hector"]);
        var both = queue.Review("acme", "widgets", "1", Verdict("hector", TaskReviewVerdict.Approve), ["lucy", "hector"]);

        Assert.Multiple(() =>
        {
            Assert.That(one!.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(both!.Stage, Is.EqualTo(TaskStage.Approved));
        });
    }

    [Test]
    public void Review_neverApprovesOnAComment()
    {
        var queue = InReview();

        // A reviewer who wrote no verdict decided nothing, even when nobody objected.
        var reviewed = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Comment), ["lucy"]);

        Assert.That(reviewed!.Stage, Is.EqualTo(TaskStage.InReview));
    }

    [Test]
    public void Review_ignoresAVerdictOnAnOlderRevision()
    {
        var queue = InReview();
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");

        var stale = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve), ["lucy"]);

        Assert.That(stale, Is.Null);
    }

    [Test]
    public void AwaitingReview_offersEachRevisionToEveryReviewerOnce()
    {
        var queue = InReview();
        queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve), ["lucy", "hector"]);

        Assert.Multiple(() =>
        {
            Assert.That(queue.AwaitingReview("lucy", "acme", "widgets"), Is.Empty);
            Assert.That(queue.AwaitingReview("hector", "acme", "widgets").Single().Id, Is.EqualTo("1"));
            Assert.That(queue.AwaitingReview("hector", "acme", "other"), Is.Empty);
        });
    }

    [Test]
    public void Held_releasesTheAuthorOnceItsTaskIsApproved()
    {
        var queue = InReview();
        queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve), ["lucy"]);

        Assert.That(queue.Held("bob"), Is.Null);
    }

    [Test]
    public void TryClaim_refusesATaskThatIsNoLongerQueued()
    {
        var queue = InReview();
        queue.Release("acme", "widgets", "1");

        Assert.That(queue.TryClaim("hector", "acme", "widgets", "1"), Is.False);
    }

    private static TaskQueueService Held(string workspaceId)
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));
        queue.TryClaim(workspaceId, "acme", "widgets", "1");
        return queue;
    }

    private static TaskQueueService InReview()
    {
        var queue = Held("bob");
        queue.Begin("bob", "acme", "widgets", "1", "main");
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");
        return queue;
    }

    private static TaskReview Verdict(string workspaceId, TaskReviewVerdict verdict) => new(workspaceId, verdict, 1, "notes");

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
