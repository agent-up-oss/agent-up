import assert from 'node:assert/strict';
import test from 'node:test';
import {
  connectionSignInSurface,
  legacySelfHostedConnection,
  parseConnectionSource,
  resolveConnectionSource,
} from './ConnectionSourceProvider';

const document = {
  apiVersion: '1',
  connectionId: 'local',
  kind: 'selfHosted',
  displayName: 'Agent-Up',
  authentication: {
    mode: 'localAdministrator',
    prompt: 'Enter the administrator password to continue.',
    identifierRequired: false,
  },
  workspacePresentation: 'serverScoped',
};

test('parseConnectionSource reads a current Server document', () => {
  const source = parseConnectionSource('http://127.0.0.1:5000', document);
  assert.equal(source.id, 'local');
  assert.equal(source.authMode, 'localAdministrator');
  assert.equal(source.isLegacy, false);
  assert.equal(connectionSignInSurface(source), 'password');
});

test('parseConnectionSource rejects an unknown apiVersion', () => {
  assert.throws(
    () => parseConnectionSource('http://127.0.0.1:5000', { ...document, apiVersion: '2' }),
    /unknown apiVersion '2'/,
  );
});

test('parseConnectionSource rejects an unknown kind', () => {
  assert.throws(
    () => parseConnectionSource('http://127.0.0.1:5000', { ...document, kind: 'hosted' }),
    /unknown kind 'hosted'/,
  );
});

test('parseConnectionSource rejects an unknown authentication.mode', () => {
  assert.throws(
    () => parseConnectionSource('http://127.0.0.1:5000', {
      ...document,
      authentication: { ...document.authentication, mode: 'magicLink' },
    }),
    /unknown authentication.mode 'magicLink'/,
  );
});

test('legacySelfHostedConnection is only a self-hosted password or open Server', () => {
  const required = legacySelfHostedConnection('http://127.0.0.1:5000', true);
  assert.equal(required.kind, 'selfHosted');
  assert.equal(required.authMode, 'localAdministrator');
  assert.equal(required.isLegacy, true);
  assert.equal(legacySelfHostedConnection('http://127.0.0.1:5000', false).authMode, 'disabled');
});

test('resolveConnectionSource uses GET /api/connection when present', async () => {
  const source = await resolveConnectionSource('http://127.0.0.1:5000', (async (url) => {
    assert.match(String(url), /\/api\/connection$/);
    return new Response(JSON.stringify(document));
  }) as typeof fetch);
  assert.equal(source.displayName, 'Agent-Up');
  assert.equal(source.isLegacy, false);
});

test('resolveConnectionSource treats a missing connection document as legacy after auth/status', async () => {
  const source = await resolveConnectionSource('http://127.0.0.1:5000', (async (url) => {
    if (String(url).endsWith('/api/connection')) return new Response('missing', { status: 404 });
    return new Response(JSON.stringify({ authenticationRequired: true }));
  }) as typeof fetch);
  assert.equal(source.isLegacy, true);
  assert.equal(source.authMode, 'localAdministrator');
});

test('resolveConnectionSource does not treat a malformed connection document as legacy', async () => {
  await assert.rejects(
    () => resolveConnectionSource('http://127.0.0.1:5000', (async () =>
      new Response(JSON.stringify({ kind: 'selfHosted' }))) as typeof fetch),
    /did not return a connection apiVersion/,
  );
});
