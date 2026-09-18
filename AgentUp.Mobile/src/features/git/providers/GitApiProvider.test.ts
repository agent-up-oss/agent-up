import assert from 'node:assert/strict';
import { test } from 'node:test';
import { checkoutRemote, commitFiles, discardFiles, fetchRemote, getChanges, getCommitQueue, getFileDiff, getHeadState, getLog, pullRemote, pushRemote, switchBranch } from './GitApiProvider';

type Recorded = { url: string; init: RequestInit };

function fakeFetch(status: number, body: string, recorded: Recorded[]): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

test('getChanges requests the workspace scoped changes route', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({
    workspaceId: 'ws 1',
    branch: 'main',
    fileCount: 0,
    root: { name: '', path: '', directories: [], files: [] },
  });

  const tree = await getChanges({ url: 'http://localhost:5000' }, 'ws 1', fakeFetch(200, body, recorded));

  assert.equal(tree?.branch, 'main');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/git/changes');
});

test('getChanges returns null for an unknown workspace', async () => {
  const tree = await getChanges({ url: 'http://localhost:5000' }, 'missing', fakeFetch(404, '', []));

  assert.equal(tree, null);
});

test('getCommitQueue exposes dependent proposals from the server', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({
    entries: [{ slice: 'Commits', message: 'feat(Commits): queue', files: ['a.cs'], id: 'entry-1', parentCommit: 'base', proposalCommit: 'tip', state: 'ready' }],
    unassignedFiles: [], queueWorktreePath: '/managed/queue', baseCommit: 'base', tipCommit: 'tip', generation: 2,
  });

  const queue = await getCommitQueue({ url: 'http://localhost:5000' }, 'ws 1', fakeFetch(200, body, recorded));

  assert.equal(queue?.entries[0].state, 'ready');
  assert.equal(queue?.generation, 2);
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/commit-queue');
});

test('getHeadState requests the workspace scoped head route', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ branch: 'main', localBranches: ['main', 'topic'] });

  const head = await getHeadState({ url: 'http://localhost:5000' }, 'ws 1', fakeFetch(200, body, recorded));

  assert.equal(head?.branch, 'main');
  assert.deepEqual(head?.localBranches, ['main', 'topic']);
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/git/head');
});

test('getFileDiff escapes the path query parameter', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ path: 'src/a b.cs', status: 'Modified', isBinary: false, diff: '@@' });

  const diff = await getFileDiff({ url: 'http://localhost:5000' }, 'ws-1', 'src/a b.cs', fakeFetch(200, body, recorded));

  assert.equal(diff?.diff, '@@');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws-1/git/file?path=src%2Fa%20b.cs');
});

test('commitFiles posts the selected files and message', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ found: true, succeeded: true, commit: '0123456789abcdef', error: null });

  const result = await commitFiles(
    { url: 'http://localhost:5000' },
    'ws-1',
    ['src/app/main.cs'],
    'feat(App): add main',
    fakeFetch(200, body, recorded),
  );

  assert.equal(result.commit, '0123456789abcdef');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws-1/git/commit');
  assert.equal(recorded[0].init.method, 'POST');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), {
    files: ['src/app/main.cs'],
    message: 'feat(App): add main',
  });
});

test('discardFiles posts the selected files', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ found: true, succeeded: true, error: null });

  const result = await discardFiles(
    { url: 'http://localhost:5000' },
    'ws-1',
    ['src/app/main.cs'],
    fakeFetch(200, body, recorded),
  );

  assert.equal(result.succeeded, true);
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws-1/git/discard');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), { files: ['src/app/main.cs'] });
});

test('switchBranch posts the requested branch name', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ found: true, succeeded: true, error: null });

  await switchBranch({ url: 'http://localhost:5000' }, 'ws-1', 'topic', true, fakeFetch(200, body, recorded));

  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws-1/git/branch');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), { name: 'topic', create: true });
});

test('checkoutRemote posts the remote branch name', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ found: true, succeeded: true, error: null });

  await checkoutRemote({ url: 'http://localhost:5000' }, 'ws-1', 'origin/topic', fakeFetch(200, body, recorded));

  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws-1/git/checkout');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), { name: 'origin/topic' });
});

test('fetchRemote posts the fetch route', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ found: true, succeeded: true, error: null, head: { branch: 'main', localBranches: ['main'] } });

  await fetchRemote({ url: 'http://localhost:5000' }, 'ws-1', 'origin', fakeFetch(200, body, recorded));

  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws-1/git/fetch');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), { remote: 'origin' });
});

test('pullRemote and pushRemote post the sync flags', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ found: true, succeeded: true, error: null, head: { branch: 'main', localBranches: ['main'] } });
  const fetchImpl = fakeFetch(200, body, recorded);

  await pullRemote({ url: 'http://localhost:5000' }, 'ws-1', true, fetchImpl);
  await pushRemote({ url: 'http://localhost:5000' }, 'ws-1', true, false, fetchImpl);

  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws-1/git/pull');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), { rebase: true });
  assert.equal(recorded[1].url, 'http://localhost:5000/api/workspaces/ws-1/git/push');
  assert.deepEqual(JSON.parse(String(recorded[1].init.body)), { forceWithLease: true, setUpstream: false });
});

test('getLog requests the bounded history route', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({
    commits: [{ id: 'abc', shortId: 'abc', parents: [], subject: 'initial', author: 'A', timestamp: '2026-01-01T00:00:00Z', refs: ['main'] }],
  });

  const log = await getLog({ url: 'http://localhost:5000' }, 'ws 1', 20, fakeFetch(200, body, recorded));

  assert.equal(log?.commits[0].subject, 'initial');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/git/log?max=20');
});

test('commitFiles surfaces the server problem detail', async () => {
  const body = JSON.stringify({ detail: 'Commit message is required.' });

  await assert.rejects(
    () => commitFiles({ url: 'http://localhost:5000' }, 'ws-1', ['a.cs'], ' ', fakeFetch(400, body, [])),
    /Commit message is required\./,
  );
});
