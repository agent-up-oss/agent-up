import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import {
  inProgress,
  offerable,
  queueIndexFor,
  waiting,
  type OfferedIssue,
  type QueuedTask,
} from '../models/TaskQueue';
import { moveTask, pickIssue, readIssues, readQueue, releaseTask, removeTask } from '../providers/TaskQueueProvider';

/** How often the Tasks tab re-reads the queue, matching the bridge's own refresh. */
const taskQueueRefreshMs = 10_000;

export type TaskQueueState = {
  /** The tasks still waiting to be taken, in the order they will be worked. */
  queue: QueuedTask[];
  /** The tasks a workspace has taken and is working now. */
  working: QueuedTask[];
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
  /** Drops a dragged task at a position among the waiting tasks, counted from zero. */
  moveTo: (task: QueuedTask, index: number) => void;
  /** Stops work on a task, which puts it back in the queue for whoever asks next. */
  stop: (task: QueuedTask) => void;
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
    let refused: string | null = null;
    try {
      await apply();
    } catch (cause) {
      refused = cause instanceof Error ? cause.message : 'The queue did not change.';
    }
    await load();
    // After the re-read, not before: the read clears the error it did not cause, so saying why a
    // change was refused first would flash the message and wipe it in the same breath.
    if (refused !== null) setError(refused);
  }, [load]);

  return {
    queue: waiting(queue),
    working: inProgress(queue),
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
    stop: task => {
      if (!server) return;
      void change(() => releaseTask(server, task));
    },
    // The screen lists only what is waiting, so the position dropped on is turned back into a
    // position in the queue the Server holds before it is sent.
    moveTo: (task, index) => {
      if (!server) return;
      void change(() => moveTask(server, task, queueIndexFor(queue, task, index)));
    },
  };
}
