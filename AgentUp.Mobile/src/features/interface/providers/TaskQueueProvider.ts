import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { OfferedIssue, QueuedTask } from '../models/TaskQueue';
import { readRepositoryIssues } from './TaskSourceProvider';

/**
 * The issues on offer and the workspace a pick is taken from. The command center's open
 * repository is separate: it asks the task source for that repository and picks with no workspace.
 */
export type OfferedIssues = { issues: OfferedIssue[]; pickFrom: string | null };

/** The open issues of the opened workspace. With no workspace open, nothing is offered. */
export async function readOffered(
  server: ServerSession,
  offeringWorkspaceId: string | null,
  request: typeof fetch = fetch,
): Promise<OfferedIssues> {
  if (offeringWorkspaceId === null) return { issues: [], pickFrom: null };
  return { issues: await readIssues(server, offeringWorkspaceId, request), pickFrom: offeringWorkspaceId };
}

/** The open issues of one repository the stored token can see. */
export async function readRepositoryOffered(
  server: ServerSession,
  moduleId: string,
  owner: string,
  repository: string,
  request: typeof fetch = fetch,
): Promise<OfferedIssue[]> {
  return await readRepositoryIssues(server, moduleId, owner, repository, request);
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

/** Picks an issue. A repository names the host the token reads; otherwise the workspace does. */
export async function pickIssue(
  server: ServerSession,
  workspaceId: string | null,
  issueId: string,
  targetWorkspaceId: string | null,
  request: typeof fetch = fetch,
  repository?: { moduleId: string; owner: string; name: string },
): Promise<void> {
  await requestServerJson<unknown>(
    server,
    '/api/tasks',
    jsonBody({
      workspaceId,
      issueId,
      targetWorkspaceId,
      moduleId: repository?.moduleId,
      owner: repository?.owner,
      repository: repository?.name,
    }),
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
