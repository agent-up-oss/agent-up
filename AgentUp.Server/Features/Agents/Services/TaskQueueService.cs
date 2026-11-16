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

            _tasks[_tasks.IndexOf(task)] = task with { HeldByWorkspaceId = workspaceId };
            Persist();
            return true;
        }
    }

    /// <summary>The task this workspace is carrying, if any.</summary>
    public QueuedTask? Held(string workspaceId)
    {
        lock (_gate)
            return _tasks.FirstOrDefault(task =>
                string.Equals(task.HeldByWorkspaceId, workspaceId, StringComparison.Ordinal));
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
