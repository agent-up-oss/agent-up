import type { GitChangeDirectory, GitChangeStatus, GitHeadState, GitLogCommit } from '@/features/git/models/GitChanges';

export type FakeGitFile = {
  path: string;
  status: GitChangeStatus;
  diff: string;
};

export type FakeGitState = {
  workspaceId: string;
  branch: string;
  commit: string;
  ahead: number;
  behind: number;
  localBranches: string[];
  remoteBranches: Array<{ remote: string; name: string }>;
  upstream: string | null;
  files: FakeGitFile[];
  log: GitLogCommit[];
  unassignedFiles: string[];
  queueWorktreePath: string | null;
  baseCommit: string;
  tipCommit: string;
  generation: number;
  entries: unknown[];
  nextCommit: number;
  nextAgentFile: number;
  incomingCommit: string | null;
  incomingPulled: boolean;
};

export type FakeGitMutation = {
  found: boolean;
  succeeded: boolean;
  error: string | null;
  commit?: string | null;
  head?: GitHeadState | null;
};

const statuses: GitChangeStatus[] = ['Modified', 'Added', 'Deleted', 'Renamed', 'Untracked', 'Conflicted'];

export function normalizeGitStatus(value: string | null | undefined): GitChangeStatus {
  if (!value) return 'Modified';
  const match = statuses.find(status => status.toLowerCase() === value.toLowerCase());
  return match ?? 'Modified';
}

export function loadFakeGitState(workspaceId: string, node: Record<string, unknown> | undefined): FakeGitState | null {
  if (!node) return null;
  const changes = asRecord(node.changes);
  const diffs = asRecord(node.diffs) ?? {};
  const log = asRecord(node.log);
  const queue = asRecord(node.queue);
  const files = collectFiles(asRecord(changes?.root), diffs);
  const commits = Array.isArray(log?.commits) ? log.commits.map(asLogCommit).filter((item): item is GitLogCommit => !!item) : [];
  const unassigned = Array.isArray(queue?.unassignedFiles)
    ? queue.unassignedFiles.filter((item): item is string => typeof item === 'string')
    : files.map(file => file.path);
  return {
    workspaceId,
    branch: stringValue(changes?.branch) ?? 'main',
    commit: stringValue(changes?.commit) ?? 'a1b2c3d',
    ahead: numberValue(changes?.ahead) ?? 0,
    behind: numberValue(changes?.behind) ?? 0,
    localBranches: stringArray(changes?.localBranches) ?? ['main'],
    remoteBranches: remoteBranches(changes?.remoteBranches),
    upstream: stringValue(changes?.upstream) ?? 'origin/main',
    files,
    log: commits,
    unassignedFiles: unassigned,
    queueWorktreePath: stringValue(queue?.queueWorktreePath),
    baseCommit: stringValue(queue?.baseCommit) ?? commits[0]?.id ?? 'a1b2c3d4e5f6',
    tipCommit: stringValue(queue?.tipCommit) ?? commits[0]?.id ?? 'a1b2c3d4e5f6',
    generation: numberValue(queue?.generation) ?? 0,
    entries: Array.isArray(queue?.entries) ? queue.entries : [],
    nextCommit: 1,
    nextAgentFile: 1,
    incomingCommit: stringValue(node.incomingCommit),
    incomingPulled: node.incomingPulled === true,
  };
}

export function gitChangesPayload(git: FakeGitState) {
  return {
    workspaceId: git.workspaceId,
    branch: git.branch,
    fileCount: git.files.length,
    commit: git.commit,
    ahead: git.ahead,
    behind: git.behind,
    localBranches: git.localBranches,
    remoteBranches: git.remoteBranches,
    upstream: git.upstream,
    root: buildChangeTree(git.files),
  };
}

export function gitHeadPayload(git: FakeGitState): GitHeadState {
  return {
    branch: git.branch,
    localBranches: [...git.localBranches],
    remoteBranches: [...git.remoteBranches],
    upstream: git.upstream,
    ahead: git.ahead,
    behind: git.behind,
    commit: git.commit,
  };
}

export function gitLogPayload(git: FakeGitState) {
  return { commits: git.log, hasMore: false };
}

export function gitQueuePayload(git: FakeGitState) {
  return {
    entries: git.entries,
    unassignedFiles: git.unassignedFiles,
    queueWorktreePath: git.queueWorktreePath,
    baseCommit: git.baseCommit,
    tipCommit: git.tipCommit,
    generation: git.generation,
  };
}

export function gitDiffPayload(git: FakeGitState, path: string): FakeGitFile | null {
  return git.files.find(file => file.path === path) ?? null;
}

export function commitGitFiles(git: FakeGitState, files: string[], message: string): FakeGitMutation {
  const selected = files.filter(path => git.files.some(file => file.path === path));
  if (selected.length === 0) return { found: true, succeeded: false, error: 'Select at least one file to commit.', commit: null };
  const commit = nextCommitId(git);
  git.files = git.files.filter(file => !selected.includes(file.path));
  git.unassignedFiles = git.unassignedFiles.filter(path => !selected.includes(path));
  git.ahead += 1;
  git.commit = commit.slice(0, 7);
  git.tipCommit = commit;
  git.log = [
    {
      id: commit,
      shortId: commit.slice(0, 7),
      parents: git.log[0] ? [git.log[0].id] : [],
      subject: message.trim() || 'chore: update harbor shop',
      author: 'Demo',
      timestamp: new Date().toISOString(),
      refs: ['HEAD', git.branch],
    },
    ...git.log.map(entry => ({ ...entry, refs: entry.refs.filter(ref => ref !== 'HEAD') })),
  ];
  fetchGitRemote(git);
  return { found: true, succeeded: true, error: null, commit, head: gitHeadPayload(git) };
}

export function discardGitFiles(git: FakeGitState, files: string[]): FakeGitMutation {
  git.files = git.files.filter(file => !files.includes(file.path));
  git.unassignedFiles = git.unassignedFiles.filter(path => !files.includes(path));
  return { found: true, succeeded: true, error: null, head: gitHeadPayload(git) };
}

export function fetchGitRemote(git: FakeGitState): FakeGitMutation {
  if (git.incomingPulled) return syncResult(git);
  if (git.behind === 0) git.behind = 1;
  ensureIncomingCommit(git);
  return syncResult(git);
}

export function pullGitRemote(git: FakeGitState): FakeGitMutation {
  fetchGitRemote(git);
  if (git.behind === 0 || git.incomingPulled) return syncResult(git);
  const commit = ensureIncomingCommit(git);
  git.behind = 0;
  git.incomingPulled = true;
  git.commit = commit.slice(0, 7);
  git.tipCommit = commit;
  git.log = [
    {
      id: commit,
      shortId: commit.slice(0, 7),
      parents: git.log[0] ? [git.log[0].id] : [],
      subject: 'chore(storefront): restock the harbor mug',
      author: 'origin',
      timestamp: new Date().toISOString(),
      refs: ['HEAD', git.branch, git.upstream ?? 'origin/main'],
    },
    ...git.log.map(entry => ({
      ...entry,
      refs: entry.refs.filter(ref => ref !== 'HEAD' && ref !== git.branch),
    })),
  ];
  return syncResult(git);
}

export function pushGitRemote(git: FakeGitState): FakeGitMutation {
  git.ahead = 0;
  return syncResult(git);
}

export function switchGitBranch(git: FakeGitState, name: string, create: boolean): FakeGitMutation {
  const branch = name.trim();
  if (!branch) return { found: true, succeeded: false, error: 'A branch name is required.' };
  if (create && !git.localBranches.includes(branch)) git.localBranches = [...git.localBranches, branch];
  if (!git.localBranches.includes(branch)) return { found: true, succeeded: false, error: `Branch ${branch} does not exist.` };
  git.branch = branch;
  return { found: true, succeeded: true, error: null, head: gitHeadPayload(git) };
}

export function checkoutGitRemote(git: FakeGitState, name: string): FakeGitMutation {
  const raw = name.trim();
  if (!raw) return { found: true, succeeded: false, error: 'A branch name is required.' };
  const local = raw.includes('/') ? raw.slice(raw.lastIndexOf('/') + 1) : raw;
  if (!git.localBranches.includes(local)) git.localBranches = [...git.localBranches, local];
  git.branch = local;
  return { found: true, succeeded: true, error: null, head: gitHeadPayload(git) };
}

export function addAgentWorkingTreeFile(git: FakeGitState): FakeGitFile {
  const suffix = git.nextAgentFile === 1 ? '' : String(git.nextAgentFile);
  git.nextAgentFile += 1;
  const path = `apps/storefront/PromoBanner${suffix}.tsx`;
  const file: FakeGitFile = {
    path,
    status: 'Added',
    diff: `--- /dev/null\n+++ b/${path}\n@@ -0,0 +1,5 @@\n+export function PromoBanner${suffix}() {\n+  return <aside>Weekly harbor special</aside>;\n+}\n`,
  };
  git.files = [...git.files.filter(item => item.path !== path), file];
  if (!git.unassignedFiles.includes(path)) git.unassignedFiles = [...git.unassignedFiles, path];
  return file;
}

export function buildChangeTree(files: FakeGitFile[]): GitChangeDirectory {
  const root: GitChangeDirectory = { name: '', path: '', directories: [], files: [] };
  for (const file of files) {
    const parts = file.path.split('/').filter(Boolean);
    let directory = root;
    for (let index = 0; index < parts.length - 1; index += 1) {
      const path = parts.slice(0, index + 1).join('/');
      let child = directory.directories.find(item => item.path === path);
      if (!child) {
        child = { name: parts[index], path, directories: [], files: [] };
        directory.directories.push(child);
      }
      directory = child;
    }
    const name = parts.at(-1) ?? file.path;
    directory.files.push({ name, path: file.path, status: file.status });
  }
  return root;
}

function collectFiles(root: Record<string, unknown> | undefined, diffs: Record<string, unknown>): FakeGitFile[] {
  const files: FakeGitFile[] = [];
  collectDirectory(root, diffs, files);
  return files;
}

function collectDirectory(
  node: Record<string, unknown> | undefined,
  diffs: Record<string, unknown>,
  files: FakeGitFile[],
): void {
  if (!node) return;
  const nested = Array.isArray(node.directories) ? node.directories : [];
  for (const child of nested) collectDirectory(asRecord(child), diffs, files);
  const listed = Array.isArray(node.files) ? node.files : [];
  for (const item of listed) {
    const file = asRecord(item);
    const path = stringValue(file?.path);
    if (!path) continue;
    const diffNode = asRecord(diffs[path]);
    files.push({
      path,
      status: normalizeGitStatus(stringValue(file?.status) ?? stringValue(diffNode?.status)),
      diff: stringValue(diffNode?.diff) ?? `--- a/${path}\n+++ b/${path}\n`,
    });
  }
}

function ensureIncomingCommit(git: FakeGitState): string {
  git.incomingCommit ??= nextCommitId(git);
  return git.incomingCommit;
}

function nextCommitId(git: FakeGitState): string {
  const id = `f4ke${String(git.nextCommit).padStart(8, '0')}`;
  git.nextCommit += 1;
  return id;
}

function syncResult(git: FakeGitState): FakeGitMutation {
  return { found: true, succeeded: true, error: null, head: gitHeadPayload(git) };
}

function asLogCommit(value: unknown): GitLogCommit | null {
  const record = asRecord(value);
  if (!record) return null;
  const id = stringValue(record.id);
  if (!id) return null;
  return {
    id,
    shortId: stringValue(record.shortId) ?? id.slice(0, 7),
    parents: stringArray(record.parents) ?? [],
    subject: stringValue(record.subject) ?? '',
    author: stringValue(record.author) ?? 'Demo',
    timestamp: stringValue(record.timestamp) ?? new Date().toISOString(),
    refs: stringArray(record.refs) ?? [],
  };
}

function remoteBranches(value: unknown): Array<{ remote: string; name: string }> {
  if (!Array.isArray(value)) return [{ remote: 'origin', name: 'main' }];
  return value
    .map(item => asRecord(item))
    .filter((item): item is Record<string, unknown> => !!item)
    .map(item => ({
      remote: stringValue(item.remote) ?? 'origin',
      name: stringValue(item.name) ?? 'main',
    }));
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : undefined;
}

function stringValue(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

function numberValue(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function stringArray(value: unknown): string[] | null {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : null;
}
