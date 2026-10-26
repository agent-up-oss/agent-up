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
 * Where a dragged row lands. The drag is measured in pixels, so the pitch between rows turns the
 * distance travelled into whole positions, and the result is clamped: dragging past either end
 * parks the row at that end rather than falling off the queue.
 *
 * The pitch is a row's height plus the gap under it, not the height alone. Measuring the height
 * and forgetting the gap makes every position short, so a long drag lands further up the queue
 * than the finger did.
 */
export function reorderTarget(fromIndex: number, dy: number, rowPitch: number, count: number): number {
  if (rowPitch <= 0 || count <= 0) return fromIndex;
  const moved = Math.round(dy / rowPitch);
  return Math.min(Math.max(fromIndex + moved, 0), count - 1);
}

/**
 * How far a row that is not the dragged one steps aside, so the queue opens a gap where the drag
 * would land rather than leaving the reader to guess.
 *
 * Only the rows between the two positions move, and each moves exactly one place: dragging down
 * pulls the rows it passes up, dragging up pushes them down. Everything outside that span stays
 * put, which is what makes the gap read as a single slot rather than the whole list sliding.
 */
export function rowShift(index: number, fromIndex: number, toIndex: number, rowPitch: number): number {
  if (index === fromIndex) return 0;
  if (toIndex > fromIndex && index > fromIndex && index <= toIndex) return -rowPitch;
  if (toIndex < fromIndex && index >= toIndex && index < fromIndex) return rowPitch;
  return 0;
}

/**
 * Where the dragged row belongs once the finger lifts. The row settles into the slot it chose
 * rather than snapping back to where it started and jumping again when the queue re-reads.
 */
export function restingOffset(fromIndex: number, toIndex: number, rowPitch: number): number {
  return (toIndex - fromIndex) * rowPitch;
}

/**
 * The issues worth offering: an issue already in the queue is not offered again, because picking
 * it twice changes nothing and showing it invites the tap that does nothing.
 */
export function offerable(issues: OfferedIssue[], queue: QueuedTask[]): OfferedIssue[] {
  const queued = new Set(queue.map(task => task.id));
  return issues.filter(issue => !queued.has(issue.id));
}
