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

    // A push is the trigger: whoever moved the branch, the approval of the code before it is stale.
    [Test]
    public void Pushed_opensANewRevisionThatNoEarlierApprovalDecides()
    {
        var queue = Held("bob");
        queue.Begin("bob", "acme", "widgets", "1", "main");
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main", "aaa");
        queue.Review("acme", "widgets", "1", Verdict("hector", TaskReviewVerdict.Approve, Dev), [Dev]);

        var pushed = queue.Pushed("acme", "widgets", "1", "bbb");
        var again = queue.Pushed("acme", "widgets", "1", "bbb");

        Assert.Multiple(() =>
        {
            Assert.That(pushed!.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(pushed.Proposal, Is.EqualTo(new TaskProposal("7", "https://example.test/pulls/7", "agent-up/issue-1", "main", 2, "bbb")));
            Assert.That(pushed.AwaitsReviewFrom("hector", Dev), Is.True);
            Assert.That(again, Is.Null, "the same head is the same revision");
        });
    }

    // A reviewer can see the author's push arrive before the author records it; that is one revision.
    [Test]
    public void Propose_keepsTheRevisionAReviewerAlreadySawPushed()
    {
        var queue = Held("bob");
        queue.Begin("bob", "acme", "widgets", "1", "main");
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main", "aaa");
        queue.Pushed("acme", "widgets", "1", "bbb");

        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main", "bbb");

        Assert.That(queue.Get("acme", "widgets", "1")!.Proposal!.Revision, Is.EqualTo(2));
    }

    [Test]
    public void Pushed_ignoresATaskWithNoProposalYet()
    {
        var queue = Held("bob");
        queue.Begin("bob", "acme", "widgets", "1", "main");

        Assert.Multiple(() =>
        {
            Assert.That(queue.Pushed("acme", "widgets", "1", "bbb"), Is.Null);
            Assert.That(queue.Proposed("acme", "widgets"), Is.Empty);
        });
    }

    [Test]
    public void Review_sendsTheTaskBackToItsAuthorOnAnyRequestForChanges()
    {
        var queue = InReview();

        var reviewed = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.RequestChanges, Pm), [Dev, Pm]);

        Assert.That(reviewed!.Stage, Is.EqualTo(TaskStage.ChangesRequested));
    }

    // The host only knows that two approvals are needed; which two is the team's.
    [Test]
    public void Review_approvesOnlyWhenEachKindOfReviewApprovesTheSameRevision()
    {
        var queue = InReview();

        var one = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve, Pm), [Dev, Pm]);
        var both = queue.Review("acme", "widgets", "1", Verdict("hector", TaskReviewVerdict.Approve, Dev), [Dev, Pm]);

        Assert.Multiple(() =>
        {
            Assert.That(one!.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(both!.Stage, Is.EqualTo(TaskStage.Approved));
            Assert.That((both.DevReviewerWorkspaceId, both.PmReviewerWorkspaceId), Is.EqualTo(("hector", "lucy")));
        });
    }

    [Test]
    public void Review_neverApprovesOnAComment()
    {
        var queue = InReview();

        // A reviewer who wrote no verdict decided nothing, even when nobody objected.
        var reviewed = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Comment, Pm), [Pm]);

        Assert.That(reviewed!.Stage, Is.EqualTo(TaskStage.InReview));
    }

    [Test]
    public void Review_ignoresAVerdictOnAnOlderRevision()
    {
        var queue = InReview();
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");

        var stale = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve, Pm), [Pm]);

        Assert.That(stale, Is.Null);
    }

    [Test]
    public void Review_ignoresAReviewOfAKindAnotherReviewerHolds()
    {
        var queue = InReview();
        queue.TryClaimReview("hector", Dev, "acme", "widgets", "1");

        var other = queue.Review("acme", "widgets", "1", Verdict("dana", TaskReviewVerdict.Approve, Dev), [Dev]);

        Assert.That(other, Is.Null);
    }

    // Both reviewers looked at the same revision; the author reads both, not only the first.
    [Test]
    public void Review_keepsAReviewThatLandsAfterChangesWereRequested()
    {
        var queue = InReview();
        queue.Review("acme", "widgets", "1", Verdict("hector", TaskReviewVerdict.RequestChanges, Dev), [Dev, Pm]);

        var late = queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve, Pm), [Dev, Pm]);

        Assert.Multiple(() =>
        {
            Assert.That(late!.Stage, Is.EqualTo(TaskStage.ChangesRequested));
            Assert.That(late.CurrentReviews.Select(review => review.WorkspaceId), Is.EqualTo(new[] { "hector", "lucy" }));
        });
    }

    [Test]
    public void AwaitingReview_offersEachRevisionToOneReviewerOfEachKind()
    {
        var queue = InReview();
        queue.TryClaimReview("hector", Dev, "acme", "widgets", "1");
        queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve, Pm), [Dev, Pm]);

        Assert.Multiple(() =>
        {
            Assert.That(queue.AwaitingReview("lucy", Pm, "acme", "widgets"), Is.Empty);
            Assert.That(queue.AwaitingReview("pat", Pm, "acme", "widgets"), Is.Empty, "the product review is done for this revision");
            Assert.That(queue.AwaitingReview("hector", Dev, "acme", "widgets").Single().Id, Is.EqualTo("1"));
            Assert.That(queue.AwaitingReview("dana", Dev, "acme", "widgets"), Is.Empty, "the dev review is hector's");
            Assert.That(queue.AwaitingReview("hector", Dev, "acme", "other"), Is.Empty);
        });
    }

    // No switching reviewers between pushes: the next revision goes back to the same two.
    [Test]
    public void TryClaimReview_keepsTheFirstReviewerOfAKindForEveryRevision()
    {
        var queue = InReview();
        var hector = queue.TryClaimReview("hector", Dev, "acme", "widgets", "1");
        var dana = queue.TryClaimReview("dana", Dev, "acme", "widgets", "1");
        queue.Review("acme", "widgets", "1", Verdict("hector", TaskReviewVerdict.RequestChanges, Dev), [Dev]);
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main");

        Assert.Multiple(() =>
        {
            Assert.That((hector, dana), Is.EqualTo((true, false)));
            Assert.That(queue.AwaitingReview("hector", Dev, "acme", "widgets").Single().Proposal!.Revision, Is.EqualTo(2));
            Assert.That(queue.AwaitingReview("dana", Dev, "acme", "widgets"), Is.Empty);
            Assert.That(queue.TryClaimReview("hector", Dev, "acme", "widgets", "1"), Is.True);
        });
    }

    [Test]
    public void TryClaimReview_refusesAnAuthor()
    {
        var queue = InReview();

        Assert.That(queue.TryClaimReview("bob", TeamMemberKind.Author, "acme", "widgets", "1"), Is.False);
    }

    [Test]
    public void NeedsReview_settlesByKindWhenAHostReviewHasNoWorkspaceHolder()
    {
        var task = Task("42", 42) with
        {
            Stage = TaskStage.InReview,
            Proposal = new TaskProposal("42", "https://example.test/42", "feature", "main", 1, "aaa"),
            Reviews = [new TaskReview("hector", TaskReviewVerdict.Approve, 1, "", Dev)],
        };

        Assert.Multiple(() =>
        {
            Assert.That(task.NeedsReview(Dev), Is.False);
            Assert.That(task.NeedsReview(Pm), Is.True);
        });
    }

    [Test]
    public void AdoptProposal_recordsAHostPullAsReviewWork()
    {
        var queue = Queue();

        var adopted = queue.AdoptProposal(Task("42", 42) with
        {
            Proposal = new TaskProposal("42", "https://example.test/42", "feature", "main", 1, "aaa"),
            BaseBranch = "main"
        });

        Assert.Multiple(() =>
        {
            Assert.That(adopted.Stage, Is.EqualTo(TaskStage.InReview));
            Assert.That(queue.AwaitingReview("hector", Dev, "acme", "widgets").Single().Id, Is.EqualTo("42"));
        });
    }

    [Test]
    public void AdoptProposal_returnsTheExistingTaskWhenTheSamePullIsAdoptedTwice()
    {
        var queue = Queue();
        var first = queue.AdoptProposal(Task("42", 42) with
        {
            Proposal = new TaskProposal("42", "https://example.test/42", "feature", "main", 1)
        });
        var second = queue.AdoptProposal(Task("42", 42) with
        {
            Proposal = new TaskProposal("42", "https://example.test/42", "feature", "main", 1)
        });

        Assert.Multiple(() =>
        {
            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(queue.List(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AdoptProposal_matchesAnExistingIssueByNumber()
    {
        var queue = Queue();
        queue.Enqueue(Task("1", 1));

        var adopted = queue.AdoptProposal(Task("pr-1", 1) with
        {
            Proposal = new TaskProposal("7", "https://example.test/7", "feature", "main", 1)
        });

        Assert.That(adopted.Id, Is.EqualTo("1"));
    }

    // A failed check is feedback: the revision goes back to its author, but only the one it ran on.
    [Test]
    public void ChecksFailed_sendsTheRevisionUnderReviewBackToItsAuthor()
    {
        var queue = Held("bob");
        queue.Begin("bob", "acme", "widgets", "1", "main");
        queue.Propose("bob", "acme", "widgets", "1", "7", "https://example.test/pulls/7", "agent-up/issue-1", "main", "aaa");

        var older = queue.ChecksFailed("acme", "widgets", "1", "zzz");
        var failed = queue.ChecksFailed("acme", "widgets", "1", "aaa");

        Assert.Multiple(() =>
        {
            Assert.That(older, Is.Null);
            Assert.That(failed!.Stage, Is.EqualTo(TaskStage.ChangesRequested));
            Assert.That(queue.Held("bob")!.Stage, Is.EqualTo(TaskStage.ChangesRequested));
        });
    }

    [Test]
    public void Held_releasesTheAuthorOnceItsTaskIsApproved()
    {
        var queue = InReview();
        queue.Review("acme", "widgets", "1", Verdict("lucy", TaskReviewVerdict.Approve, Pm), [Pm]);

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

    private const TeamMemberKind Dev = TeamMemberKind.DevReviewer;

    private const TeamMemberKind Pm = TeamMemberKind.PmReviewer;

    private static TaskReview Verdict(string workspaceId, TaskReviewVerdict verdict, TeamMemberKind kind) => new(workspaceId, verdict, 1, "notes", kind);

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
