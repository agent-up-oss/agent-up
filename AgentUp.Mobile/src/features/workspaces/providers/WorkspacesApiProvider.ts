import { jsonBody, requestServerJson, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { CloneSourceRequest, Workspace } from '../models/Workspace';

// Cloning a repository can take minutes on a large remote, so it gets its own timeout.
const CLONE_TIMEOUT_MS = 180000;

export async function listWorkspaces(server: ServerSession, request: typeof fetch = fetch): Promise<Workspace[]> {
  return (await requestServerJson<Workspace[]>(server, '/api/workspaces', { method: 'GET' }, undefined, request)) ?? [];
}

export async function startWorkspace(server: ServerSession, workspaceId: string, request: typeof fetch = fetch): Promise<void> {
  await requestServerJson(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/start`,
    { method: 'POST' },
    undefined,
    request,
  );
}

export async function stopWorkspace(server: ServerSession, workspaceId: string, request: typeof fetch = fetch): Promise<void> {
  await requestServerJson(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/stop`,
    { method: 'POST' },
    undefined,
    request,
  );
}

export async function cloneSourceRepository(
  server: ServerSession,
  body: CloneSourceRequest,
  request: typeof fetch = fetch,
): Promise<Workspace> {
  const workspace = await requestServerJson<Workspace>(server, '/api/source-clones', jsonBody(body), CLONE_TIMEOUT_MS, request);
  if (!workspace) throw new Error('The server did not return the cloned workspace.');
  return workspace;
}
