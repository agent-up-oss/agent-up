import { jsonBody, requestServerJson, ServerRequestError, type ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { WorkspaceDirectoryListing, WorkspaceFileContent } from '../models/WorkspaceFiles';

/** What a save came to: the saved file, or why not. A conflict is a file someone changed since it was opened. */
export type WorkspaceFileSave =
  | { ok: true; file: WorkspaceFileContent }
  | { ok: false; conflict: boolean; error: string };

function base(workspaceId: string): string {
  return `/api/workspaces/${encodeURIComponent(workspaceId)}/files`;
}

/** One folder, one level deep, or null when the workspace or folder is gone. */
export async function getDirectory(
  server: ServerSession,
  workspaceId: string,
  path: string,
  request: typeof fetch = fetch,
): Promise<WorkspaceDirectoryListing | null> {
  return readOrNull(server, `${base(workspaceId)}/directory?path=${encodeURIComponent(path)}`, request);
}

/** One whole file, or null when the workspace or file is gone. */
export async function getFile(
  server: ServerSession,
  workspaceId: string,
  path: string,
  request: typeof fetch = fetch,
): Promise<WorkspaceFileContent | null> {
  return readOrNull(server, `${base(workspaceId)}/file?path=${encodeURIComponent(path)}`, request);
}

/** Saves an edit made from `version`; the Server refuses it if the file moved on since. */
export async function saveFile(
  server: ServerSession,
  workspaceId: string,
  path: string,
  content: string,
  version: string,
  request: typeof fetch = fetch,
): Promise<WorkspaceFileSave> {
  try {
    const file = await requestServerJson<WorkspaceFileContent>(
      server,
      `${base(workspaceId)}/file`,
      { ...jsonBody({ path, content, version }), method: 'PUT' },
      undefined,
      request,
    );
    return file ? { ok: true, file } : { ok: false, conflict: false, error: 'The Server returned an empty save.' };
  } catch (error) {
    if (!(error instanceof ServerRequestError)) throw error;
    return { ok: false, conflict: isStale(error), error: error.message };
  }
}

/** A stale save: the problem detail says the file changed since it was opened. A busy agent is a 409 too, and is not one. */
function isStale(error: ServerRequestError): boolean {
  return error.status === 409 && /changed on the Server/i.test(error.message);
}

async function readOrNull<T>(server: ServerSession, path: string, request: typeof fetch): Promise<T | null> {
  try {
    return await requestServerJson<T>(server, path, { method: 'GET' }, undefined, request);
  } catch (error) {
    if (error instanceof ServerRequestError && error.status === 404) return null;
    throw error;
  }
}
