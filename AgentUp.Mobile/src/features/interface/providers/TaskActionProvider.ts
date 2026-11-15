import type { OfferedIssue, QueuedTask } from '../models/TaskQueue';

/** What the Tasks board can do to the Server's queue. */
export type TaskActions = {
  queue: QueuedTask[];
  working: QueuedTask[];
  offered: OfferedIssue[];
  pick(issue: OfferedIssue): void;
  remove(task: QueuedTask): void;
  stop(task: QueuedTask): void;
};

/**
 * Runs a task row's scene action against the queue. The scene names the action each row offers
 * (`queue`, `dequeue`, `stop`); this turns that name and the row id back into the change.
 * Returns whether the action was a task action at all, so the host can try its other handlers.
 */
export function runTaskAction(name: string, id: string | null, tasks: TaskActions): boolean {
  if (name === 'dequeue') {
    const task = tasks.queue.find(item => item.id === id);
    if (task) tasks.remove(task);
    return true;
  }
  if (name === 'stop') {
    const task = tasks.working.find(item => item.id === id);
    if (task) tasks.stop(task);
    return true;
  }
  if (name === 'queue') {
    const issue = tasks.offered.find(item => item.id === id);
    if (issue) tasks.pick(issue);
    return true;
  }
  return false;
}
