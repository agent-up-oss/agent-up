import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { TaskSource } from '../models/TaskSource';
import type { OfferedIssue } from '../models/TaskQueue';

/** Where this Server's agents take their work from. */
export async function readTaskSource(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<TaskSource | null> {
  return await requestServerJson<TaskSource>(server, '/api/task-source', undefined, undefined, request);
}

export async function setTaskSource(
  server: ServerSession,
  moduleId: string,
  owner: string,
  repository: string,
  request: typeof fetch = fetch,
): Promise<TaskSource | null> {
  return await requestServerJson<TaskSource>(
    server,
    '/api/task-source',
    { ...jsonBody({ moduleId, owner, repository }), method: 'PUT' },
    undefined,
    request,
  );
}

export async function clearTaskSource(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<TaskSource | null> {
  return await requestServerJson<TaskSource>(
    server,
    '/api/task-source',
    { method: 'DELETE' },
    undefined,
    request,
  );
}

/** Where to send the reader to authorize. */
export async function startTaskSourceLogin(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<string | null> {
  const started = await requestServerJson<{ authorizeUrl: string }>(
    server,
    '/api/task-source/login',
    { method: 'POST' },
    undefined,
    request,
  );
  return started?.authorizeUrl ?? null;
}

/** The open issues the configured source serves. */
export async function readSourceIssues(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<OfferedIssue[]> {
  return (await requestServerJson<OfferedIssue[]>(
    server,
    '/api/task-source/issues',
    undefined,
    undefined,
    request,
  )) ?? [];
}
