import assert from 'node:assert/strict';
import { test } from 'node:test';
import { commitFiles, discardFiles, getChanges, getFileDiff, getHeadState, switchBranch } from './GitApiProvider';

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

test('commitFiles surfaces the server problem detail', async () => {
  const body = JSON.stringify({ detail: 'Commit message is required.' });

  await assert.rejects(
    () => commitFiles({ url: 'http://localhost:5000' }, 'ws-1', ['a.cs'], ' ', fakeFetch(400, body, [])),
    /Commit message is required\./,
  );
});
