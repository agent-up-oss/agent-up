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

/**
 * Who may take a task: one agent by name, or anyone on the repository. A name is used where the
 * team knows it, because a workspace id says nothing to the person reading the queue.
 */
export function targetLabel(task: QueuedTask, names: Map<string, string>): string {
  return task.targetWorkspaceId === null ? 'anyone' : names.get(task.targetWorkspaceId) ?? task.targetWorkspaceId;
}

/** Who has it now. A free task says so rather than leaving the column blank. */
export function holderLabel(task: QueuedTask, names: Map<string, string>): string {
  return task.heldByWorkspaceId === null ? 'free' : names.get(task.heldByWorkspaceId) ?? task.heldByWorkspaceId;
}

/**
 * The issues worth offering: an issue already in the queue is not offered again, because picking
 * it twice changes nothing and showing it invites the tap that does nothing.
 */
export function offerable(issues: OfferedIssue[], queue: QueuedTask[]): OfferedIssue[] {
  const queued = new Set(queue.map(task => task.id));
  return issues.filter(issue => !queued.has(issue.id));
}
