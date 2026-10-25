import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import { offerable, type OfferedIssue, type QueuedTask } from '../models/TaskQueue';
import { moveTask, pickIssue, readIssues, readQueue, releaseTask, removeTask } from '../providers/TaskQueueProvider';

/** How often the Tasks tab re-reads the queue, matching the bridge's own refresh. */
const taskQueueRefreshMs = 10_000;

export type TaskQueueState = {
  /** The queue in the order it will be worked. */
  queue: QueuedTask[];
  /** Open issues on the shown workspace's repository that are not queued yet. */
  offered: OfferedIssue[];
  /** Workspace id to display name, so a row can name who holds a task. */
  names: Map<string, string>;
  loading: boolean;
  read: boolean;
  error: string | null;
  refresh: () => void;
  pick: (issue: OfferedIssue) => void;
  remove: (task: QueuedTask) => void;
  release: (task: QueuedTask) => void;
  move: (task: QueuedTask, delta: number) => void;
};

/**
 * The queue the agents work from, and the issues you can add to it.
 *
 * The Server owns the queue and decides every claim, so nothing is edited here: a change is sent
 * and the list is read back. A change the Server refused must not leave the screen saying it
 * happened.
 */
export function useTaskQueue(): TaskQueueState {
  const { server, workspaces, error: workspaceError } = useWorkspaces();
  const [queue, setQueue] = useState<QueuedTask[]>([]);
  const [issues, setIssues] = useState<OfferedIssue[]>([]);
  const [loading, setLoading] = useState(false);
  const [read, setRead] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The issues on offer belong to a repository, and every workspace here is a worktree of one, so
  // the first workspace is enough to ask which issues are open.
  const offeringWorkspaceId = workspaces[0]?.id ?? null;
  const names = useMemo(
    () => new Map(workspaces.map(workspace => [workspace.id, workspace.displayName])),
    [workspaces],
  );

  // Reads overlap: the timer, a pull, and every change start one. Nothing cancels the read already
  // out, so only the newest may report, or a slower earlier one lands last and draws a stale queue.
  const newest = useRef(0);

  const load = useCallback(async () => {
    if (!server) return;
    const ticket = ++newest.current;
    setLoading(true);
    try {
      const [queued, open] = await Promise.all([
        readQueue(server),
        offeringWorkspaceId === null ? Promise.resolve([]) : readIssues(server, offeringWorkspaceId),
      ]);
      if (ticket !== newest.current) return;
      setQueue(queued);
      setIssues(open);
      setError(null);
      setRead(true);
    } catch (cause) {
      if (ticket !== newest.current) return;
      setError(cause instanceof Error ? cause.message : 'Could not read the queue.');
    } finally {
      if (ticket === newest.current) setLoading(false);
    }
  }, [server, offeringWorkspaceId]);

  useEffect(() => {
    void load();
    const timer = setInterval(() => { void load(); }, taskQueueRefreshMs);
    return () => clearInterval(timer);
  }, [load]);

  const change = useCallback(async (apply: () => Promise<void>) => {
    try {
      await apply();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'The queue did not change.');
    }
    await load();
  }, [load]);

  return {
    queue,
    offered: offerable(issues, queue),
    names,
    loading,
    read,
    error: error ?? workspaceError,
    refresh: () => { void load(); },
    pick: issue => {
      if (!server || offeringWorkspaceId === null) return;
      void change(() => pickIssue(server, offeringWorkspaceId, issue.id, null));
    },
    remove: task => {
      if (!server) return;
      void change(() => removeTask(server, task));
    },
    release: task => {
      if (!server) return;
      void change(() => releaseTask(server, task));
    },
    move: (task, delta) => {
      if (!server) return;
      const index = queue.findIndex(item => item.id === task.id);
      if (index < 0) return;
      void change(() => moveTask(server, task, index + delta));
    },
  };
}
