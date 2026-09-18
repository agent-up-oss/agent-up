import { jsonBody, requestServerJson, ServerRequestError, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { GitChangeTree, GitCommitResult, GitFileDiff, GitHeadState, GitLog, GitMutationResult, GitSyncResult } from '../models/GitChanges';
import type { CommitQueue } from '../models/CommitQueue';

const COMMIT_TIMEOUT_MS = 60000;

export async function getChanges(
  server: ServerSession,
  workspaceId: string,
  request: typeof fetch = fetch,
): Promise<GitChangeTree | null> {
  return readOrNull<GitChangeTree>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/changes`,
    request,
  );
}

export async function getCommitQueue(
  server: ServerSession,
  workspaceId: string,
  request: typeof fetch = fetch,
): Promise<CommitQueue | null> {
  return readOrNull<CommitQueue>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/commit-queue`,
    request,
  );
}

export async function getHeadState(
  server: ServerSession,
  workspaceId: string,
  request: typeof fetch = fetch,
): Promise<GitHeadState | null> {
  return readOrNull<GitHeadState>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/head`,
    request,
  );
}

export async function getFileDiff(
  server: ServerSession,
  workspaceId: string,
  path: string,
  request: typeof fetch = fetch,
): Promise<GitFileDiff | null> {
  return readOrNull<GitFileDiff>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/file?path=${encodeURIComponent(path)}`,
    request,
  );
}

export async function commitFiles(
  server: ServerSession,
  workspaceId: string,
  files: string[],
  message: string,
  request: typeof fetch = fetch,
): Promise<GitCommitResult> {
  const result = await requestServerJson<GitCommitResult>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/commit`,
    jsonBody({ files, message }),
    COMMIT_TIMEOUT_MS,
    request,
  );
  if (!result) throw new Error('The server returned an empty commit result.');
  return result;
}

export async function discardFiles(
  server: ServerSession,
  workspaceId: string,
  files: string[],
  request: typeof fetch = fetch,
): Promise<GitMutationResult> {
  const result = await requestServerJson<GitMutationResult>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/discard`,
    jsonBody({ files }),
    COMMIT_TIMEOUT_MS,
    request,
  );
  if (!result) throw new Error('The server returned an empty discard result.');
  return result;
}

export async function switchBranch(
  server: ServerSession,
  workspaceId: string,
  name: string,
  create = false,
  request: typeof fetch = fetch,
): Promise<GitMutationResult> {
  const result = await requestServerJson<GitMutationResult>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/branch`,
    jsonBody({ name, create }),
    COMMIT_TIMEOUT_MS,
    request,
  );
  if (!result) throw new Error('The server returned an empty branch result.');
  return result;
}

export async function checkoutRemote(
  server: ServerSession,
  workspaceId: string,
  name: string,
  request: typeof fetch = fetch,
): Promise<GitMutationResult> {
  const result = await requestServerJson<GitMutationResult>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/checkout`,
    jsonBody({ name }),
    COMMIT_TIMEOUT_MS,
    request,
  );
  if (!result) throw new Error('The server returned an empty checkout result.');
  return result;
}

export async function fetchRemote(
  server: ServerSession,
  workspaceId: string,
  remote: string | null = null,
  request: typeof fetch = fetch,
): Promise<GitSyncResult> {
  return postSync(server, workspaceId, 'fetch', { remote }, request);
}

export async function pullRemote(
  server: ServerSession,
  workspaceId: string,
  rebase = false,
  request: typeof fetch = fetch,
): Promise<GitSyncResult> {
  return postSync(server, workspaceId, 'pull', { rebase }, request);
}

export async function pushRemote(
  server: ServerSession,
  workspaceId: string,
  forceWithLease = false,
  setUpstream = false,
  request: typeof fetch = fetch,
): Promise<GitSyncResult> {
  return postSync(server, workspaceId, 'push', { forceWithLease, setUpstream }, request);
}

export async function getLog(
  server: ServerSession,
  workspaceId: string,
  max = 100,
  request: typeof fetch = fetch,
): Promise<GitLog | null> {
  return readOrNull<GitLog>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/log?max=${max}`,
    request,
  );
}

async function postSync(
  server: ServerSession,
  workspaceId: string,
  action: string,
  body: object,
  request: typeof fetch,
): Promise<GitSyncResult> {
  const result = await requestServerJson<GitSyncResult>(
    server,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/${action}`,
    jsonBody(body),
    COMMIT_TIMEOUT_MS,
    request,
  );
  if (!result) throw new Error(`The server returned an empty ${action} result.`);
  return result;
}

async function readOrNull<T>(server: ServerSession, path: string, request: typeof fetch): Promise<T | null> {
  try {
    return await requestServerJson<T>(server, path, { method: 'GET' }, undefined, request);
  } catch (error) {
    if (error instanceof ServerRequestError && error.status === 404) return null;
    throw error;
  }
}
