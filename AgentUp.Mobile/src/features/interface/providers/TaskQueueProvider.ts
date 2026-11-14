import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { OfferedIssue, QueuedTask } from '../models/TaskQueue';

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

export async function pickIssue(
  server: ServerSession,
  workspaceId: string,
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
