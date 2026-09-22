import assert from 'node:assert/strict';
import test from 'node:test';
import { loadFakeServerDefinition } from '../providers/FakeServerDefinitionProvider';
import { FakeBackendService } from './FakeBackendService';

function backend() {
  return new FakeBackendService(loadFakeServerDefinition());
}

test('authentication and entitlements do not require a password', () => {
  const service = backend();
  const status = JSON.parse(service.handle({ method: 'GET', path: '/api/auth/status', query: '', body: null }).body!);
  const login = JSON.parse(service.handle({ method: 'POST', path: '/api/auth/login', query: '', body: '{}' }).body!);
  const entitlements = JSON.parse(service.handle({ method: 'GET', path: '/api/entitlements', query: '', body: null }).body!);
  assert.equal(status.authenticationRequired, false);
  assert.equal(login.accessToken, 'fake-token');
  assert.equal(entitlements.edition, 'community');
});

test('lists the bundled Harbor Shop workspace', () => {
  const service = backend();
  const workspaces = JSON.parse(service.handle({ method: 'GET', path: '/api/workspaces', query: '', body: null }).body!);
  assert.equal(workspaces[0].id, 'harbor-shop');
  assert.equal(workspaces[0].state, 'Running');
});

test('agent prompts publish the scripted demo events', () => {
  const service = backend();
  const types: string[] = [];
  const unsubscribe = service.subscribeAgent('harbor-shop', event => types.push(event.type));
  const result = service.handle({
    method: 'POST',
    path: '/api/workspaces/harbor-shop/agent/messages',
    query: '',
    body: JSON.stringify({ message: 'status?' }),
  });
  unsubscribe();
  assert.equal(result.status, 204);
  assert.ok(types.includes('user_message'));
  assert.ok(types.includes('session_update'));
});

test('application tickets serve the bundled storefront HTML', () => {
  const service = backend();
  const ticket = JSON.parse(service.handle({
    method: 'POST',
    path: '/api/apps/tickets',
    query: '',
    body: JSON.stringify({ workspaceId: 'harbor-shop', allocatedPort: 9100 }),
  }).body!);
  const page = service.handle({ method: 'GET', path: ticket.bootstrapPath, query: '', body: null });
  assert.equal(ticket.bootstrapPath, '/apps/harbor-shop/storefront');
  assert.match(page.contentType, /^text\/html/);
  assert.match(page.body ?? '', /Harbor Mug/);
  assert.match(service.applicationHtml(9100) ?? '', /Harbor Shop/);
});

test('capability modules stay empty on Demo', () => {
  const service = backend();
  const listed = JSON.parse(service.handle({ method: 'GET', path: '/api/capabilities', query: '', body: null }).body!);
  const enabled = JSON.parse(service.handle({
    method: 'POST',
    path: '/api/capabilities/enable',
    query: '',
    body: JSON.stringify({ id: 'dotnet' }),
  }).body!);
  assert.equal(listed.length, 0);
  assert.equal(enabled.enabled, true);
  assert.equal(enabled.canRun, false);
});

test('reset restores the bundled workspace after a clone', () => {
  const service = backend();
  service.handle({
    method: 'POST',
    path: '/api/source-clones',
    query: '',
    body: JSON.stringify({ repository: 'https://git.example/widgets.git' }),
  });
  assert.equal(JSON.parse(service.handle({ method: 'GET', path: '/api/workspaces', query: '', body: null }).body!).length, 2);
  service.reset();
  assert.equal(JSON.parse(service.handle({ method: 'GET', path: '/api/workspaces', query: '', body: null }).body!).length, 1);
});
