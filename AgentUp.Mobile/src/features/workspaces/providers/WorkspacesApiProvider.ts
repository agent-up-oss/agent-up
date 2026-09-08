import { jsonBody, readServerJson, sendServerRequest } from '@/features/servers/providers/ServerRequestProvider';
import type { CloneSourceRequest, Workspace } from '../models/Workspace';

// Cloning a repository can take minutes on a large remote, so it gets its own timeout.
const CLONE_TIMEOUT_MS = 180000;

export async function listWorkspaces(serverUrl: string, request: typeof fetch = fetch): Promise<Workspace[]> {
  const response = await sendServerRequest(serverUrl, '/api/workspaces', { method: 'GET' }, undefined, request);
  return (await readServerJson<Workspace[]>(response)) ?? [];
}

export async function startWorkspace(serverUrl: string, workspaceId: string, request: typeof fetch = fetch): Promise<void> {
  await sendServerRequest(
    serverUrl,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/start`,
    { method: 'POST' },
    undefined,
    request,
  );
}

export async function stopWorkspace(serverUrl: string, workspaceId: string, request: typeof fetch = fetch): Promise<void> {
  await sendServerRequest(
    serverUrl,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/stop`,
    { method: 'POST' },
    undefined,
    request,
  );
}

export async function cloneSourceRepository(
  serverUrl: string,
  body: CloneSourceRequest,
  request: typeof fetch = fetch,
): Promise<Workspace> {
  const response = await sendServerRequest(serverUrl, '/api/source-clones', jsonBody(body), CLONE_TIMEOUT_MS, request);
  const workspace = await readServerJson<Workspace>(response);
  if (!workspace) throw new Error('The server did not return the cloned workspace.');
  return workspace;
}
