import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cloneSourceRepository, getReviewReportHtml, listWorkspaces, refreshClone, removeWorkspace, startWorkspace, stopWorkspace } from './WorkspacesApiProvider';

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

  const workspaces = await listWorkspaces({ url: 'http://localhost:5000' }, fakeFetch(200, body, recorded));

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
    { url: 'http://localhost:5000' },
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
      { url: 'http://localhost:5000' },
      { repository: 'https://example.test/acme/widgets.git', branch: '--x' },
      fakeFetch(400, body, []),
    ),
    /Branch must be a valid Git branch name\./,
  );
});

test('cloneSourceRepository posts a derived review workspace at a revision', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({
    id: 'ws-review',
    displayName: 'Review · topic',
    repositoryPath: '/clones/widgets-review-topic',
    worktreePath: '/clones/widgets-review-topic',
    branch: 'topic',
    commit: 'abc123',
    state: 'Stopped',
    derived: true,
    sourceRepository: 'https://example.test/acme/widgets.git',
    sourceRevision: 'topic',
  });

  const workspace = await cloneSourceRepository(
    { url: 'http://localhost:5000' },
    {
      repository: 'https://example.test/acme/widgets.git',
      branch: 'topic',
      revision: 'topic',
      derived: true,
    },
    fakeFetch(201, body, recorded),
  );

  assert.equal(workspace.derived, true);
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), {
    repository: 'https://example.test/acme/widgets.git',
    branch: 'topic',
    revision: 'topic',
    derived: true,
  });
});

test('refreshClone posts to the source clone refresh route', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({
    id: 'ws 1',
    displayName: 'Review · topic',
    repositoryPath: '/clones/widgets-review-topic',
    worktreePath: '/clones/widgets-review-topic',
    branch: 'topic',
    commit: 'refreshed',
    state: 'Stopped',
    derived: true,
  });

  const workspace = await refreshClone({ url: 'http://localhost:5000' }, 'ws 1', undefined, fakeFetch(200, body, recorded));

  assert.equal(workspace.commit, 'refreshed');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/source-clones/ws%201/refresh');
  assert.equal(recorded[0].init.body, undefined);
});

test('refreshClone posts a different revision when one is named', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({
    id: 'ws-1',
    displayName: 'Review · release',
    repositoryPath: '/clones/widgets-review-topic',
    worktreePath: '/clones/widgets-review-topic',
    branch: 'release',
    commit: 'refreshed',
    state: 'Stopped',
    derived: true,
    sourceRevision: 'release',
  });

  const workspace = await refreshClone({ url: 'http://localhost:5000' }, 'ws-1', 'release', fakeFetch(200, body, recorded));

  assert.equal(workspace.sourceRevision, 'release');
  assert.equal(JSON.parse(String(recorded[0].init.body)).revision, 'release');
});

test('removeWorkspace deletes the workspace', async () => {
  const recorded: Recorded[] = [];

  await removeWorkspace({ url: 'http://localhost:5000' }, 'ws 1', fakeFetch(204, '', recorded));

  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201');
  assert.equal(recorded[0].init.method, 'DELETE');
});

test('getReviewReportHtml reads the HTML report route', async () => {
  const recorded: Recorded[] = [];
  const fetcher = (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    return new Response('<html>Harbor Shop</html>', { status: 200, headers: { 'Content-Type': 'text/html' } });
  }) as unknown as typeof fetch;

  const html = await getReviewReportHtml({ url: 'http://localhost:5000' }, 'ws 1', fetcher);

  assert.equal(html, '<html>Harbor Shop</html>');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/review-snapshot');
});

test('startWorkspace and stopWorkspace escape the workspace id', async () => {
  const recorded: Recorded[] = [];
  const fetcher = fakeFetch(204, '', recorded);

  await startWorkspace({ url: 'http://localhost:5000' }, 'ws 1', fetcher);
  await stopWorkspace({ url: 'http://localhost:5000' }, 'ws 1', fetcher);

  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/start');
  assert.equal(recorded[1].url, 'http://localhost:5000/api/workspaces/ws%201/stop');
});
