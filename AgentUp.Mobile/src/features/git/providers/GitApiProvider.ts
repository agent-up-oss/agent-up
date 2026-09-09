import { jsonBody, requestServerJson, ServerRequestError, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { GitChangeTree, GitCommitResult, GitFileDiff } from '../models/GitChanges';

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

async function readOrNull<T>(server: ServerSession, path: string, request: typeof fetch): Promise<T | null> {
  try {
    return await requestServerJson<T>(server, path, { method: 'GET' }, undefined, request);
  } catch (error) {
    if (error instanceof ServerRequestError && error.status === 404) return null;
    throw error;
  }
}
