import type { GitChangeStatus } from '@/features/git/models/GitChanges';
import type {
  WorkspaceDirectoryListing,
  WorkspaceFileContent,
  WorkspaceFileEntry,
} from '@/features/files/models/WorkspaceFiles';
import type { FakeGitFile } from './FakeGitProvider';

/**
 * The Demo's own files: the Harbor Shop repository the definition carries, what this workspace's
 * Git state says changed, and the edits made in this session. A file the agent added exists only
 * in its diff, so its text is read back from the added lines.
 */
export type FakeFilesInput = {
  workspaceId: string;
  repository: Record<string, string>;
  edits: Map<string, string>;
  changes: FakeGitFile[];
};

export type FakeFilesResult<T> =
  | { ok: true; value: T }
  | { ok: false; status: 400 | 404 | 409; title: string; detail: string };

export function fakeDirectoryListing(input: FakeFilesInput, rawPath: string | null): FakeFilesResult<WorkspaceDirectoryListing> {
  const path = normalize(rawPath);
  if (path === null) return invalid(rawPath);
  const files = effectiveFiles(input);
  const prefix = path ? `${path}/` : '';
  if (path && !Object.keys(files).some(key => key.startsWith(prefix))) {
    return { ok: false, status: 404, title: 'Not found', detail: `'${path}' is not a folder in this workspace.` };
  }
  const statuses = statusMap(input.changes);
  const directories = new Set<string>();
  const entries: WorkspaceFileEntry[] = [];
  for (const key of Object.keys(files).filter(item => item.startsWith(prefix))) {
    const rest = key.slice(prefix.length);
    const slash = rest.indexOf('/');
    if (slash >= 0) {
      directories.add(rest.slice(0, slash));
      continue;
    }
    entries.push({
      name: rest,
      path: key,
      kind: 'File',
      size: byteLength(files[key]),
      status: statuses.get(key) ?? null,
      changedFiles: 0,
    });
  }
  const folders: WorkspaceFileEntry[] = [...directories].map(name => {
    const folderPath = prefix + name;
    return {
      name,
      path: folderPath,
      kind: 'Directory',
      size: null,
      status: null,
      changedFiles: input.changes.filter(change => change.status !== 'Deleted' && change.path.startsWith(`${folderPath}/`)).length,
    };
  });
  const byName = (a: WorkspaceFileEntry, b: WorkspaceFileEntry) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  return {
    ok: true,
    value: { workspaceId: input.workspaceId, path, entries: [...folders.sort(byName), ...entries.sort(byName)], truncated: false },
  };
}

export function fakeFileContent(input: FakeFilesInput, rawPath: string | null): FakeFilesResult<WorkspaceFileContent> {
  const path = normalize(rawPath);
  if (!path) return invalid(rawPath);
  const text = effectiveFiles(input)[path];
  if (text === undefined) return { ok: false, status: 404, title: 'Not found', detail: `'${path}' is not a file in this workspace.` };
  return { ok: true, value: describe(path, text, statusMap(input.changes).get(path) ?? null) };
}

/**
 * Saves an edit when it was made from the text that is there now. The file then reads as
 * modified in Git, with a diff of the whole file, so the Demo's Git tab shows the edit too.
 */
export function writeFakeFile(
  input: FakeFilesInput,
  body: { path?: unknown; content?: unknown; version?: unknown },
): FakeFilesResult<{ content: WorkspaceFileContent; change: FakeGitFile | null }> {
  const current = fakeFileContent(input, typeof body.path === 'string' ? body.path : null);
  if (!current.ok) return current;
  if (typeof body.content !== 'string' || typeof body.version !== 'string') {
    return { ok: false, status: 400, title: 'Invalid file request', detail: 'A save names the file, its new content, and the version it was edited from.' };
  }
  if (body.version !== current.value.version) {
    return {
      ok: false,
      status: 409,
      title: 'File changed on the Server',
      detail: `'${current.value.path}' changed on the Server since it was opened. Reload it to see the change.`,
    };
  }
  const path = current.value.path;
  const original = input.repository[path];
  input.edits.set(path, body.content);
  const existing = input.changes.find(change => change.path === path);
  const change: FakeGitFile | null = existing
    ? null
    : { path, status: 'Modified', diff: wholeFileDiff(path, original ?? '', body.content) };
  const status = existing?.status ?? 'Modified';
  return { ok: true, value: { content: describe(path, body.content, status), change } };
}

/** Names exact text, so a save can prove it started from what is there now. */
export function fakeFileVersion(text: string): string {
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return `demo-${hash.toString(16).padStart(8, '0')}-${text.length}`;
}

function describe(path: string, text: string, status: GitChangeStatus | null): WorkspaceFileContent {
  return {
    path,
    size: byteLength(text),
    version: fakeFileVersion(text),
    status,
    isBinary: false,
    isTooLarge: false,
    content: text,
  };
}

function effectiveFiles(input: FakeFilesInput): Record<string, string> {
  const files: Record<string, string> = { ...input.repository };
  for (const change of input.changes) {
    if (change.status === 'Deleted') delete files[change.path];
    else if (files[change.path] === undefined) files[change.path] = addedText(change.diff);
  }
  for (const [path, text] of input.edits) files[path] = text;
  return files;
}

function statusMap(changes: FakeGitFile[]): Map<string, GitChangeStatus> {
  return new Map(changes.filter(change => change.status !== 'Deleted').map(change => [change.path, change.status]));
}

function addedText(diff: string): string {
  return diff
    .split('\n')
    .filter(line => line.startsWith('+') && !line.startsWith('+++'))
    .map(line => `${line.slice(1)}\n`)
    .join('');
}

function wholeFileDiff(path: string, before: string, after: string): string {
  const lines = (text: string) => (text.length === 0 ? [] : text.replace(/\n$/, '').split('\n'));
  const old = lines(before);
  const next = lines(after);
  return [
    `--- a/${path}`,
    `+++ b/${path}`,
    `@@ -1,${old.length} +1,${next.length} @@`,
    ...old.map(line => `-${line}`),
    ...next.map(line => `+${line}`),
    '',
  ].join('\n');
}

function normalize(path: string | null): string | null {
  const segments = (path ?? '').split('/').filter(segment => segment.length > 0 && segment !== '.');
  if (segments.some(segment => segment === '..' || segment.toLowerCase() === '.git')) return null;
  return segments.join('/');
}

function invalid(path: string | null): FakeFilesResult<never> {
  return { ok: false, status: 400, title: 'Invalid file request', detail: `'${path ?? ''}' is not a workspace path.` };
}

function byteLength(text: string): number {
  return new TextEncoder().encode(text).length;
}
