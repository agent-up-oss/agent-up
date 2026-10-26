/**
 * The tasks somebody picked for the agents. Nothing reaches an agent by being open on the host:
 * an issue is worked on because it is here, in the order it sits here.
 */
export type QueuedTask = {
  id: string;
  owner: string;
  repository: string;
  number: number;
  title: string;
  url: string;
  /** The one workspace allowed to take this, or null to let whoever asks next take it. */
  targetWorkspaceId: string | null;
  /** The workspace carrying it, or null while it is still free. */
  heldByWorkspaceId: string | null;
};

/** An open issue on a workspace's repository, offered for picking. */
export type OfferedIssue = {
  id: string;
  number: number;
  title: string;
  url: string;
};

/** How a row names the task, as the queue and the host both label it. */
export function taskLabel(task: QueuedTask): string {
  return `#${task.number} · ${task.title}`;
}

export function issueLabel(issue: OfferedIssue): string {
  return `#${issue.number} · ${issue.title}`;
}

/** The repository a task belongs to, as the host names it. */
export function repositoryLabel(task: QueuedTask): string {
  return `${task.owner}/${task.repository}`;
}

/**
 * Where a dragged row lands. The drag is measured in pixels, so the row height turns the distance
 * travelled into whole positions, and the result is clamped: dragging past either end parks the
 * row at that end rather than falling off the queue.
 */
export function reorderTarget(fromIndex: number, dy: number, rowHeight: number, count: number): number {
  if (rowHeight <= 0 || count <= 0) return fromIndex;
  const moved = Math.round(dy / rowHeight);
  return Math.min(Math.max(fromIndex + moved, 0), count - 1);
}

/**
 * The issues worth offering: an issue already in the queue is not offered again, because picking
 * it twice changes nothing and showing it invites the tap that does nothing.
 */
export function offerable(issues: OfferedIssue[], queue: QueuedTask[]): OfferedIssue[] {
  const queued = new Set(queue.map(task => task.id));
  return issues.filter(issue => !queued.has(issue.id));
}
