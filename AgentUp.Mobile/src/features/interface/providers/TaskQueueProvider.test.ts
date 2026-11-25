import assert from 'node:assert/strict';
import { test } from 'node:test';
import { pickIssue, readOffered, readRepositoryOffered } from './TaskQueueProvider';

type Recorded = { url: string; init: RequestInit };

function routedFetch(routes: Record<string, unknown>, recorded: Recorded[]): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    const path = new URL(String(url)).pathname;
    const body = routes[path];
    return new Response(body === undefined ? '' : JSON.stringify(body), {
      status: body === undefined ? 404 : 200,
      headers: { 'Content-Type': 'application/json' },
    });
  }) as unknown as typeof fetch;
}

const server = { url: 'http://localhost:5000' };
const issue = { id: 'I-57', number: 57, title: 'Cart badge lags behind the cart', url: 'u' };

test('readRepositoryOffered asks the stored token for that repository', async () => {
  const recorded: Recorded[] = [];
  const issues = await readRepositoryOffered(server, 'github', 'example', 'repository', routedFetch({
    '/api/task-source/issues': [issue],
  }, recorded));

  assert.deepEqual(issues.map(item => item.number), [57]);
  assert.equal(recorded[0].url.includes('moduleId=github'), true);
  assert.equal(recorded[0].url.includes('owner=example'), true);
});

test('readOffered reads the workspace repository and picks from that workspace', async () => {
  const offered = await readOffered(server, 'bob', routedFetch({
    '/api/workspaces/bob/forge/issues': [issue],
  }, []));

  assert.deepEqual(offered.issues.map(item => item.number), [57]);
  assert.equal(offered.pickFrom, 'bob');
});

test('readOffered offers nothing when no workspace is open', async () => {
  const recorded: Recorded[] = [];
  const offered = await readOffered(server, null, routedFetch({}, recorded));

  assert.deepEqual(offered, { issues: [], pickFrom: null });
  assert.equal(recorded.length, 0);
});

test('pickIssue sends a null workspace for a task source issue', async () => {
  const recorded: Recorded[] = [];
  await pickIssue(server, null, 'I-57', null, routedFetch({ '/api/tasks': {} }, recorded));

  assert.equal(recorded[0].init.method, 'POST');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), { workspaceId: null, issueId: 'I-57', targetWorkspaceId: null });
});
