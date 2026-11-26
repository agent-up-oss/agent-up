import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { OfferedIssue } from '../models/TaskQueue';
import type { TaskSource, TaskSourcePull, TaskSourceRepository } from '../models/TaskSource';

/** The forge connections, without their tokens. */
export async function readTaskSource(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<TaskSource | null> {
  return await requestServerJson<TaskSource>(server, '/api/task-source', undefined, undefined, request);
}

/** Stores a personal access token and host URL for one team member. The Server checks them and does not return the token. */
export async function saveTaskSourceToken(
  server: ServerSession,
  workspaceId: string,
  moduleId: string,
  endpointUrl: string,
  token: string,
  request: typeof fetch = fetch,
): Promise<TaskSource | null> {
  return await requestServerJson<TaskSource>(
    server,
    '/api/task-source',
    { ...jsonBody({ workspaceId, moduleId, endpointUrl, token }), method: 'PUT' },
    undefined,
    request,
  );
}

/** Forgets the token stored for one team member. */
export async function forgetTaskSource(
  server: ServerSession,
  workspaceId: string,
  request: typeof fetch = fetch,
): Promise<TaskSource | null> {
  return await requestServerJson<TaskSource>(
    server,
    `/api/task-source/${encodeURIComponent(workspaceId)}`,
    { method: 'DELETE' },
    undefined,
    request,
  );
}

/** Every repository the stored tokens can see. */
export async function readRepositories(
  server: ServerSession,
  request: typeof fetch = fetch,
): Promise<TaskSourceRepository[]> {
  return (await requestServerJson<TaskSourceRepository[]>(
    server,
    '/api/task-source/repositories',
    undefined,
    undefined,
    request,
  )) ?? [];
}

/** The open issues of one repository. */
export async function readRepositoryIssues(
  server: ServerSession,
  moduleId: string,
  owner: string,
  repository: string,
  request: typeof fetch = fetch,
): Promise<OfferedIssue[]> {
  const query = new URLSearchParams({ moduleId, owner, repository });
  return (await requestServerJson<OfferedIssue[]>(
    server,
    `/api/task-source/issues?${query}`,
    undefined,
    undefined,
    request,
  )) ?? [];
}

/** The open pull requests of one repository, with the reviews inferred from them. */
export async function readRepositoryPulls(
  server: ServerSession,
  moduleId: string,
  owner: string,
  repository: string,
  request: typeof fetch = fetch,
): Promise<TaskSourcePull[]> {
  const query = new URLSearchParams({ moduleId, owner, repository });
  return (await requestServerJson<TaskSourcePull[]>(
    server,
    `/api/task-source/pulls?${query}`,
    undefined,
    undefined,
    request,
  )) ?? [];
}
