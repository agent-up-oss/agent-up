import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cloneSourceRepository, listWorkspaces, startWorkspace, stopWorkspace } from './WorkspacesApiProvider';

type Recorded = { url: string; init: RequestInit };

function fakeFetch(status: number, body: string, recorded: Recorded[]): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    // 204 responses must not carry a body.
    return new Response(status === 204 ? null : body, { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

test('listWorkspaces reads the workspace list', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify([
    {
      id: 'ws-1',
      displayName: 'widgets',
      repositoryPath: '/clones/widgets',
      worktreePath: '/clones/widgets',
      branch: 'main',
      commit: 'abc123',
      state: 'Stopped',
    },
  ]);

  const workspaces = await listWorkspaces('http://localhost:5000', fakeFetch(200, body, recorded));

  assert.equal(workspaces.length, 1);
  assert.equal(workspaces[0].displayName, 'widgets');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces');
});

test('cloneSourceRepository posts the repository and branch to the source clones route', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({
    id: 'ws-1',
    displayName: 'widgets',
    repositoryPath: '/clones/widgets',
    worktreePath: '/clones/widgets',
    branch: 'main',
    commit: 'abc123',
    state: 'Stopped',
  });

  const workspace = await cloneSourceRepository(
    'http://localhost:5000',
    { repository: 'https://example.test/acme/widgets.git', branch: 'main' },
    fakeFetch(201, body, recorded),
  );

  assert.equal(workspace.id, 'ws-1');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/source-clones');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), {
    repository: 'https://example.test/acme/widgets.git',
    branch: 'main',
  });
});

test('cloneSourceRepository surfaces the server problem detail', async () => {
  const body = JSON.stringify({ detail: 'Branch must be a valid Git branch name.' });

  await assert.rejects(
    () => cloneSourceRepository(
      'http://localhost:5000',
      { repository: 'https://example.test/acme/widgets.git', branch: '--x' },
      fakeFetch(400, body, []),
    ),
    /Branch must be a valid Git branch name\./,
  );
});

test('startWorkspace and stopWorkspace escape the workspace id', async () => {
  const recorded: Recorded[] = [];
  const fetcher = fakeFetch(204, '', recorded);

  await startWorkspace('http://localhost:5000', 'ws 1', fetcher);
  await stopWorkspace('http://localhost:5000', 'ws 1', fetcher);

  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/start');
  assert.equal(recorded[1].url, 'http://localhost:5000/api/workspaces/ws%201/stop');
});
