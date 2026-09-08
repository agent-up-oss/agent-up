import { jsonBody, requestServerJson, ServerRequestError } from '@/features/servers/providers/ServerRequestProvider';
import type { GitChangeTree, GitCommitResult, GitFileDiff } from '../models/GitChanges';

const COMMIT_TIMEOUT_MS = 60000;

export async function getChanges(
  serverUrl: string,
  workspaceId: string,
  request: typeof fetch = fetch,
): Promise<GitChangeTree | null> {
  return readOrNull<GitChangeTree>(
    serverUrl,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/changes`,
    request,
  );
}

export async function getFileDiff(
  serverUrl: string,
  workspaceId: string,
  path: string,
  request: typeof fetch = fetch,
): Promise<GitFileDiff | null> {
  return readOrNull<GitFileDiff>(
    serverUrl,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/file?path=${encodeURIComponent(path)}`,
    request,
  );
}

export async function commitFiles(
  serverUrl: string,
  workspaceId: string,
  files: string[],
  message: string,
  request: typeof fetch = fetch,
): Promise<GitCommitResult> {
  const result = await requestServerJson<GitCommitResult>(
    serverUrl,
    `/api/workspaces/${encodeURIComponent(workspaceId)}/git/commit`,
    jsonBody({ files, message }),
    COMMIT_TIMEOUT_MS,
    request,
  );
  if (!result) throw new Error('The server returned an empty commit result.');
  return result;
}

async function readOrNull<T>(serverUrl: string, path: string, request: typeof fetch): Promise<T | null> {
  try {
    return await requestServerJson<T>(serverUrl, path, { method: 'GET' }, undefined, request);
  } catch (error) {
    if (error instanceof ServerRequestError && error.status === 404) return null;
    throw error;
  }
}
