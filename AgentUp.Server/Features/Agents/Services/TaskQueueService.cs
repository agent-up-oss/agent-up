using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// The one queue the autonomous workspaces draw from. An issue being open on the host means
/// nothing here: somebody picks it, and only then can a workspace take it.
///
/// Every workspace pops from the same queue, so the claim has to be decided in one place. Taking
/// a task is a single locked step — read, choose, mark held, persist — because two workspaces
/// asking at the same moment must not walk away with the same issue.
/// </summary>
public sealed class TaskQueueService
{
    private readonly ITaskQueueStore _store;
    private readonly List<QueuedTask> _tasks;
    private readonly object _gate = new();

    public TaskQueueService(ITaskQueueStore store)
    {
        _store = store;
        _tasks = [.. store.Read()];
    }

    /// <summary>The whole queue in the order it will be worked.</summary>
    public IReadOnlyList<QueuedTask> List()
    {
        lock (_gate)
            return [.. _tasks];
    }

    /// <summary>
    /// Puts an issue at the back of the queue. Picking the same issue twice is a no-op rather than
    /// an error: the queue is a set of decisions, and deciding twice changes nothing.
    /// </summary>
    public QueuedTask Enqueue(QueuedTask task)
    {
        lock (_gate)
        {
            var existing = Find(task.Owner, task.Repository, task.Id);
            if (existing is not null)
                return existing;

            _tasks.Add(task);
            Persist();
            return task;
        }
    }

    /// <summary>Takes an issue back out. A task somebody is carrying is not removed silently.</summary>
    public bool Remove(string owner, string repository, string id, bool evenWhenHeld = false)
    {
        lock (_gate)
        {
            var task = Find(owner, repository, id);
            if (task is null || (task.HeldByWorkspaceId is not null && !evenWhenHeld))
                return false;

            _tasks.Remove(task);
            Persist();
            return true;
        }
    }

    /// <summary>Moves a task to a position in the queue, clamped to the ends.</summary>
    public bool Move(string owner, string repository, string id, int index)
    {
        lock (_gate)
        {
            var task = Find(owner, repository, id);
            if (task is null)
                return false;

            _tasks.Remove(task);
            _tasks.Insert(Math.Clamp(index, 0, _tasks.Count), task);
            Persist();
            return true;
        }
    }

    /// <summary>
    /// Points a task at one workspace, or at anyone when the id is null. A task somebody already
    /// holds keeps its holder: retargeting decides who may take it, not who is on it.
    /// </summary>
    public bool Target(string owner, string repository, string id, string? workspaceId)
    {
        lock (_gate)
            return Replace(owner, repository, id, task => task with { TargetWorkspaceId = workspaceId });
    }

    /// <summary>
    /// Takes one named task for this workspace. False means somebody else holds it now, or it is
    /// not offered here, and the caller should choose again rather than work it anyway.
    ///
    /// The whole decision happens under the lock, so of two workspaces reaching for the same issue
    /// exactly one walks away with it. A workspace that already holds this task keeps it, which is
    /// what lets a blocked or restarted run resume the task it owes instead of taking a new one.
    /// </summary>
    public bool TryClaim(string workspaceId, string owner, string repository, string id)
    {
        lock (_gate)
        {
            var task = Find(owner, repository, id);
            if (task is null || !task.OfferedTo(workspaceId, owner, repository))
                return false;

            if (task.HeldByWorkspaceId is not null)
                return string.Equals(task.HeldByWorkspaceId, workspaceId, StringComparison.Ordinal);
            if (task.Stage != TaskStage.Queued)
                return false;

            _tasks[_tasks.IndexOf(task)] = task with { HeldByWorkspaceId = workspaceId };
            Persist();
            return true;
        }
    }

    /// <summary>
    /// The task this workspace is carrying, if any. An approved task is no longer carried: its
    /// author is free for the next one while the host decides whether to merge.
    /// </summary>
    public QueuedTask? Held(string workspaceId)
    {
        lock (_gate)
            return _tasks.FirstOrDefault(task =>
                string.Equals(task.HeldByWorkspaceId, workspaceId, StringComparison.Ordinal)
                && task.Stage != TaskStage.Approved);
    }

    /// <summary>One task by its host id within a repository.</summary>
    public QueuedTask? Get(string owner, string repository, string id)
    {
        lock (_gate)
            return Find(owner, repository, id);
    }

    /// <summary>The author has started changing the code for a task it holds, from this base branch.</summary>
    public bool Begin(string workspaceId, string owner, string repository, string id, string baseBranch)
    {
        lock (_gate)
            return ReplaceHeld(workspaceId, owner, repository, id, task => task.Stage is TaskStage.Queued
                ? task with { Stage = TaskStage.Implementing, BaseBranch = task.BaseBranch ?? baseBranch }
                : task);
    }

    /// <summary>
    /// The author's work is on the host as a proposal. Each push is a new revision, and every
    /// reviewer owes a review of it, whatever they said about the one before. A head a reviewer
    /// already saw arrive is that same revision, not another.
    /// </summary>
    public bool Propose(string workspaceId, string owner, string repository, string id, string number, string url, string branch, string baseBranch, string? head = null)
    {
        lock (_gate)
            return ReplaceHeld(workspaceId, owner, repository, id, task => task with
            {
                Stage = TaskStage.InReview,
                Proposal = new TaskProposal(
                    number,
                    url,
                    branch,
                    baseBranch,
                    head is not null && task.Proposal?.Head == head ? task.Proposal.Revision : (task.Proposal?.Revision ?? 0) + 1,
                    head)
            });
    }

    /// <summary>
    /// The proposal's branch moved on the host. Whoever pushed - the author's loop or a person -
    /// that is a new revision: it goes back in front of every reviewer, and no verdict on the code
    /// before it decides anything. Returns the task when the push opened a revision.
    /// </summary>
    public QueuedTask? Pushed(string owner, string repository, string id, string head)
    {
        lock (_gate)
        {
            var task = Find(owner, repository, id);
            if (task?.Proposal is null
                || task.Stage is not (TaskStage.InReview or TaskStage.ChangesRequested or TaskStage.Approved)
                || string.Equals(task.Proposal.Head, head, StringComparison.Ordinal))
                return null;

            var next = task with
            {
                Stage = TaskStage.InReview,
                Proposal = task.Proposal with { Revision = task.Proposal.Revision + 1, Head = head }
            };
            _tasks[_tasks.IndexOf(task)] = next;
            Persist();
            return next;
        }
    }

    /// <summary>The tasks on a repository that have a proposal open on the host, in queue order.</summary>
    public IReadOnlyList<QueuedTask> Proposed(string owner, string repository)
    {
        lock (_gate)
            return [.. _tasks.Where(task => task.Proposal is not null
                && task.Stage is TaskStage.InReview or TaskStage.ChangesRequested or TaskStage.Approved
                && string.Equals(task.Owner, owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(task.Repository, repository, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// The tasks whose open revision needs a review of this reviewer's kind and that are free or
    /// already this reviewer's, in queue order.
    /// </summary>
    public IReadOnlyList<QueuedTask> AwaitingReview(string workspaceId, TeamMemberKind kind, string owner, string repository)
    {
        lock (_gate)
            return [.. _tasks.Where(task => task.AwaitsReviewFrom(workspaceId, kind)
                && string.Equals(task.Owner, owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(task.Repository, repository, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// Takes a task's review of this kind for one reviewer. The first reviewer of a kind keeps it
    /// for every later revision, so of two reviewers reaching for the same review exactly one gets
    /// it, and nobody else of that kind is offered the task again. True for the reviewer that
    /// already holds it.
    /// </summary>
    public bool TryClaimReview(string workspaceId, TeamMemberKind kind, string owner, string repository, string id)
    {
        lock (_gate)
        {
            var task = Find(owner, repository, id);
            if (task is null || kind is not (TeamMemberKind.DevReviewer or TeamMemberKind.PmReviewer))
                return false;
            var holder = task.ReviewerFor(kind);
            if (holder is not null)
                return string.Equals(holder, workspaceId, StringComparison.Ordinal);
            if (task.Stage != TaskStage.InReview)
                return false;

            _tasks[_tasks.IndexOf(task)] = task.WithReviewer(kind, workspaceId);
            Persist();
            return true;
        }
    }

    /// <summary>
    /// Records one reviewer's verdict on the open revision. Any request for changes sends the task
    /// back to its author at once; it is approved only when the reviewer holding each required
    /// kind of review approved the same revision. A comment decides nothing. A review of a kind
    /// another reviewer holds is not this reviewer's to give and is ignored.
    ///
    /// A review that lands after another reviewer already sent the revision back is still kept:
    /// the author reads every finding on the revision, not only the first.
    /// </summary>
    public QueuedTask? Review(string owner, string repository, string id, TaskReview review, IReadOnlyCollection<TeamMemberKind> required)
    {
        lock (_gate)
        {
            var task = Find(owner, repository, id);
            if (task?.Proposal is null
                || task.Stage is not (TaskStage.InReview or TaskStage.ChangesRequested)
                || review.Revision != task.Proposal.Revision)
                return null;
            var holder = task.ReviewerFor(review.Kind);
            if (holder is not null && !string.Equals(holder, review.WorkspaceId, StringComparison.Ordinal))
                return null;

            var reviewed = (holder is null ? task.WithReviewer(review.Kind, review.WorkspaceId) : task)
                with { Reviews = [.. (task.Reviews ?? []), review] };
            var current = reviewed.CurrentReviews;
            var stage = task.Stage == TaskStage.ChangesRequested || current.Any(item => item.Verdict == TaskReviewVerdict.RequestChanges)
                ? TaskStage.ChangesRequested
                : required.Count > 0 && required.All(kind => current.Any(item =>
                    string.Equals(item.WorkspaceId, reviewed.ReviewerFor(kind), StringComparison.Ordinal) && item.Verdict == TaskReviewVerdict.Approve))
                    ? TaskStage.Approved
                    : TaskStage.InReview;
            var next = reviewed with { Stage = stage };
            _tasks[_tasks.IndexOf(task)] = next;
            Persist();
            return next;
        }
    }

    /// <summary>
    /// The host's checks failed on the revision under review. That is feedback like a request for
    /// changes: the task goes back to its author, whose next push is the next revision. A failure
    /// on an older head decides nothing.
    /// </summary>
    public QueuedTask? ChecksFailed(string owner, string repository, string id, string head)
    {
        lock (_gate)
        {
            var task = Find(owner, repository, id);
            if (task?.Proposal is null
                || task.Stage != TaskStage.InReview
                || !string.Equals(task.Proposal.Head, head, StringComparison.Ordinal))
                return null;

            var next = task with { Stage = TaskStage.ChangesRequested };
            _tasks[_tasks.IndexOf(task)] = next;
            Persist();
            return next;
        }
    }

    /// <summary>Takes a finished task out of the queue. Only its holder can finish it.</summary>
    public bool Complete(string workspaceId, string id)
    {
        lock (_gate)
        {
            var task = _tasks.FirstOrDefault(item =>
                item.Id == id && string.Equals(item.HeldByWorkspaceId, workspaceId, StringComparison.Ordinal));
            if (task is null)
                return false;

            _tasks.Remove(task);
            Persist();
            return true;
        }
    }

    /// <summary>
    /// Hands a held task back to the queue. Nothing in the loop does this on its own — a hold is
    /// meant to survive failure — so it is the deliberate way to move work off a stuck workspace.
    /// </summary>
    public bool Release(string owner, string repository, string id)
    {
        lock (_gate)
            return Replace(owner, repository, id, task => task with { HeldByWorkspaceId = null });
    }

    private bool ReplaceHeld(string workspaceId, string owner, string repository, string id, Func<QueuedTask, QueuedTask> change)
    {
        var task = Find(owner, repository, id);
        if (task is null || !string.Equals(task.HeldByWorkspaceId, workspaceId, StringComparison.Ordinal))
            return false;

        _tasks[_tasks.IndexOf(task)] = change(task);
        Persist();
        return true;
    }

    private bool Replace(string owner, string repository, string id, Func<QueuedTask, QueuedTask> change)
    {
        var task = Find(owner, repository, id);
        if (task is null)
            return false;

        _tasks[_tasks.IndexOf(task)] = change(task);
        Persist();
        return true;
    }

    private QueuedTask? Find(string owner, string repository, string id) =>
        _tasks.FirstOrDefault(task => task.Id == id
            && string.Equals(task.Owner, owner, StringComparison.OrdinalIgnoreCase)
            && string.Equals(task.Repository, repository, StringComparison.OrdinalIgnoreCase));

    private void Persist() => _store.Write([.. _tasks]);
}
