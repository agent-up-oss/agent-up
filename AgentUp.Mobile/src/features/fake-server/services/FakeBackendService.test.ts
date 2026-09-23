import assert from 'node:assert/strict';
import test from 'node:test';
import { loadFakeServerDefinition } from '../providers/FakeServerDefinitionProvider';
import { FakeBackendService, type FakeScheduler } from './FakeBackendService';

function backend(schedule?: FakeScheduler) {
  return new FakeBackendService(loadFakeServerDefinition(), schedule);
}

function queuedBackend() {
  const queue: Array<() => void> = [];
  const service = backend((_ms, work) => {
    queue.push(work);
    return () => {
      const index = queue.indexOf(work);
      if (index >= 0) queue.splice(index, 1);
    };
  });
  return {
    service,
    step() {
      queue.shift()?.();
    },
  };
}

function json(service: FakeBackendService, method: string, path: string, body: string | null = null) {
  return JSON.parse(service.handle({ method, path, query: '', body }).body!);
}

test('authentication and entitlements do not require a password', () => {
  const service = backend();
  const status = json(service, 'GET', '/api/auth/status');
  const login = json(service, 'POST', '/api/auth/login', '{}');
  const entitlements = json(service, 'GET', '/api/entitlements');
  assert.equal(status.authenticationRequired, false);
  assert.equal(login.accessToken, 'fake-token');
  assert.equal(entitlements.edition, 'community');
});

test('lists the bundled Harbor Shop workspace', () => {
  const service = backend();
  const workspaces = json(service, 'GET', '/api/workspaces');
  assert.equal(workspaces[0].id, 'harbor-shop');
  assert.equal(workspaces[0].state, 'Running');
});

test('agent prompts add a working-tree file and say so', () => {
  const service = backend();
  const types: string[] = [];
  const texts: string[] = [];
  const unsubscribe = service.subscribeAgent('harbor-shop', event => {
    types.push(event.type);
    const payload = event.payload as { content?: { text?: string } };
    if (payload?.content?.text) texts.push(payload.content.text);
  });
  const result = service.handle({
    method: 'POST',
    path: '/api/workspaces/harbor-shop/agent/messages',
    query: '',
    body: JSON.stringify({ message: 'add a promo banner' }),
  });
  unsubscribe();
  const changes = json(service, 'GET', '/api/workspaces/harbor-shop/git/changes');
  assert.equal(result.status, 204);
  assert.ok(types.includes('user_message'));
  assert.ok(types.includes('session_update'));
  assert.match(texts.join('\n'), /PromoBanner/);
  assert.equal(changes.fileCount, 3);
});

test('application tickets serve interactive storefront HTML', () => {
  const service = backend();
  const ticket = json(service, 'POST', '/api/apps/tickets', JSON.stringify({ workspaceId: 'harbor-shop', allocatedPort: 9100 }));
  const page = service.handle({ method: 'GET', path: ticket.bootstrapPath, query: '', body: null });
  const orders = service.handle({ method: 'GET', path: '/apps/harbor-shop/orders-api', query: '', body: null });
  assert.equal(ticket.bootstrapPath, '/apps/harbor-shop/storefront');
  assert.match(page.contentType, /^text\/html/);
  assert.match(page.body ?? '', /Place order/);
  assert.match(orders.body ?? '', /Create order/);
  assert.doesNotMatch(orders.body ?? '', /bundled with the client/);
});

test('capability modules list first-party packages and toggle', () => {
  const service = backend();
  const listed = json(service, 'GET', '/api/capabilities');
  const disabled = json(service, 'POST', '/api/capabilities/disable/dotnet');
  const enabled = json(service, 'POST', '/api/capabilities/enable', JSON.stringify({ id: 'dotnet' }));
  assert.deepEqual(listed.map((item: { id: string }) => item.id), ['dotnet', 'docker', 'codex', 'cursor', 'claude']);
  assert.equal(disabled.enabled, false);
  assert.equal(enabled.enabled, true);
  assert.equal(enabled.canRun, true);
});

test('agent list follows enabled agent capabilities', () => {
  const service = backend();
  const listed = json(service, 'GET', '/api/workspaces/harbor-shop/agent');
  json(service, 'POST', '/api/capabilities/disable/claude');
  const afterDisable = json(service, 'GET', '/api/workspaces/harbor-shop/agent');
  const rejected = service.handle({
    method: 'POST',
    path: '/api/workspaces/harbor-shop/agent',
    query: '',
    body: JSON.stringify({ agent: 'claude' }),
  });
  json(service, 'POST', '/api/capabilities/enable', JSON.stringify({ id: 'claude' }));
  const scheduled = json(service, 'POST', '/api/workspaces/harbor-shop/agent', JSON.stringify({ agent: 'claude' }));
  assert.deepEqual(listed.agents.map((item: { agent: string }) => item.agent), ['codex', 'cursor', 'claude']);
  assert.deepEqual(afterDisable.agents.map((item: { agent: string }) => item.agent), ['codex', 'cursor']);
  assert.equal(rejected.status, 409);
  assert.equal(scheduled.agent, 'claude');
});

test('git head commit discard and fetch mutate the demo tree', () => {
  const service = backend();
  const head = json(service, 'GET', '/api/workspaces/harbor-shop/git/head');
  const committed = json(service, 'POST', '/api/workspaces/harbor-shop/git/commit', JSON.stringify({
    files: ['apps/storefront/ProductGrid.tsx'],
    message: 'fix(storefront): featured grid',
  }));
  const afterCommit = json(service, 'GET', '/api/workspaces/harbor-shop/git/changes');
  const fetched = json(service, 'POST', '/api/workspaces/harbor-shop/git/fetch', '{}');
  assert.equal(head.branch, 'main');
  assert.equal(committed.succeeded, true);
  assert.equal(committed.head.ahead, 1);
  assert.equal(committed.head.behind, 1);
  assert.equal(afterCommit.fileCount, 1);
  assert.equal(fetched.head.behind, 1);
});

test('git pull integrates the incoming remote commit only once', () => {
  const service = backend();
  json(service, 'POST', '/api/workspaces/harbor-shop/git/fetch', '{}');
  json(service, 'POST', '/api/workspaces/harbor-shop/git/pull', '{}');
  const secondFetch = json(service, 'POST', '/api/workspaces/harbor-shop/git/fetch', '{}');
  const secondPull = json(service, 'POST', '/api/workspaces/harbor-shop/git/pull', '{}');
  const log = json(service, 'GET', '/api/workspaces/harbor-shop/git/log');
  assert.equal(secondFetch.head.behind, 0);
  assert.equal(secondPull.head.behind, 0);
  assert.equal(log.commits.filter((entry: { author: string }) => entry.author === 'origin').length, 1);
});

test('start walks starting checking then healthy and stop keeps apps offline', () => {
  const { service, step } = queuedBackend();
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/harbor-shop/stop', query: '', body: null }).status, 204);
  const stopped = json(service, 'GET', '/api/workspaces/harbor-shop');
  assert.equal(stopped.state, 'Stopped');
  assert.equal(stopped.healthState, undefined);
  assert.equal(stopped.applications.length, 2);
  assert.equal(stopped.applications.every((item: { state: string }) => item.state === 'Stopped'), true);

  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/harbor-shop/start', query: '', body: null }).status, 204);
  assert.equal(json(service, 'GET', '/api/workspaces/harbor-shop').state, 'Starting');
  step();
  const checking = json(service, 'GET', '/api/workspaces/harbor-shop');
  assert.equal(checking.state, 'Running');
  assert.equal(checking.healthState, 'Checking');
  step();
  const healthy = json(service, 'GET', '/api/workspaces/harbor-shop');
  assert.equal(healthy.healthState, 'Healthy');
  assert.equal(healthy.applications.length, 2);
});

test('reset restores the bundled workspace after a clone', () => {
  const service = backend();
  service.handle({
    method: 'POST',
    path: '/api/source-clones',
    query: '',
    body: JSON.stringify({ repository: 'https://git.example/widgets.git' }),
  });
  assert.equal(json(service, 'GET', '/api/workspaces').length, 2);
  service.reset();
  assert.equal(json(service, 'GET', '/api/workspaces').length, 1);
});
