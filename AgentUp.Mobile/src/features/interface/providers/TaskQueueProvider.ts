import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { OfferedIssue, QueuedTask } from '../models/TaskQueue';
import { readSourceIssues, readTaskSource } from './TaskSourceProvider';

/**
 * The issues on offer and where a pick is taken from. `pickFrom` is null when the Server's task
 * source offered them: the Server then picks from that source rather than from a workspace.
 */
export type OfferedIssues = { issues: OfferedIssue[]; pickFrom: string | null };

/**
 * The issues a reader can pick from. A configured source answers for the whole Server. Without
 * one, the opened workspace's own repository answers, which is what every Server did before a
 * source could be named.
 */
export async function readOffered(
  server: ServerSession,
  offeringWorkspaceId: string | null,
  request: typeof fetch = fetch,
): Promise<OfferedIssues> {
  const source = await readTaskSource(server, request);
  if (source?.configured) return { issues: await readSourceIssues(server, request), pickFrom: null };
  if (offeringWorkspaceId === null) return { issues: [], pickFrom: null };
  return { issues: await readIssues(server, offeringWorkspaceId, request), pickFrom: offeringWorkspaceId };
}

/** The whole picked queue, in the order it will be worked. */
export async function readQueue(server: ServerSession, request: typeof fetch = fetch): Promise<QueuedTask[]> {
  return (await requestServerJson<QueuedTask[]>(server, '/api/tasks', undefined, undefined, request)) ?? [];
}

/** Every open issue on a workspace's repository, whether or not anyone is assigned to it. */
export async function readIssues(
  server: ServerSession,
  workspaceId: string,
  request: typeof fetch = fetch,
): Promise<OfferedIssue[]> {
  return (await requestServerJson<OfferedIssue[]>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/forge/issues`,
    undefined,
    undefined,
    request,
  )) ?? [];
}

/** Picks an issue. A null workspace means the Server's task source. */
export async function pickIssue(
  server: ServerSession,
  workspaceId: string | null,
  issueId: string,
  targetWorkspaceId: string | null,
  request: typeof fetch = fetch,
): Promise<void> {
  await requestServerJson<unknown>(
    server,
    '/api/tasks',
    jsonBody({ workspaceId, issueId, targetWorkspaceId }),
    undefined,
    request,
  );
}

export async function removeTask(server: ServerSession, task: QueuedTask, request: typeof fetch = fetch): Promise<void> {
  await requestServerJson<unknown>(server, root(task), { method: 'DELETE' }, undefined, request);
}

export async function releaseTask(server: ServerSession, task: QueuedTask, request: typeof fetch = fetch): Promise<void> {
  await requestServerJson<unknown>(server, `${root(task)}/release`, { method: 'POST' }, undefined, request);
}

export async function moveTask(
  server: ServerSession,
  task: QueuedTask,
  index: number,
  request: typeof fetch = fetch,
): Promise<void> {
  await requestServerJson<unknown>(
    server,
    `${root(task)}/position`,
    { ...jsonBody({ index }), method: 'PUT' },
    undefined,
    request,
  );
}

function root(task: QueuedTask): string {
  return `/api/tasks/${encodeURIComponent(task.owner)}/${encodeURIComponent(task.repository)}/${encodeURIComponent(task.id)}`;
}
