import assert from 'node:assert/strict';
import test from 'node:test';
import { isLoopbackRedirect, startBrowserSsoExample } from './server.mjs';

test('isLoopbackRedirect accepts only loopback http(s) callbacks', () => {
  assert.equal(isLoopbackRedirect('http://127.0.0.1:8081/connect'), true);
  assert.equal(isLoopbackRedirect('http://localhost:8081/connect'), true);
  assert.equal(isLoopbackRedirect('https://example.com/connect'), false);
  assert.equal(isLoopbackRedirect('not a url'), false);
});

test('the example advertises browserSso and issues a restricted entitlement document', async () => {
  const example = await startBrowserSsoExample();
  try {
    const connection = await getJson(example.origin, '/api/connection');
    assert.equal(connection.kind, 'selfHosted');
    assert.equal(connection.authentication.mode, 'browserSso');

    const unauthorized = await fetch(`${example.origin}/api/entitlements`);
    assert.equal(unauthorized.status, 401);

    const rejected = await fetch(`${example.origin}/api/auth/sso?redirect_uri=${encodeURIComponent('https://example.com/connect')}`);
    assert.equal(rejected.status, 400);

    const start = await fetch(`${example.origin}/api/auth/sso?redirect_uri=${encodeURIComponent('http://127.0.0.1:8081/connect')}`);
    assert.equal(start.status, 200);
    const html = await start.text();
    assert.match(html, /Sign in to Shared Server/);
    assert.match(html, /id="sso-continue"/);

    const complete = await fetch(`${example.origin}/api/auth/sso`, {
      method: 'POST',
      redirect: 'manual',
      headers: { 'content-type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ redirect_uri: 'http://127.0.0.1:8081/connect' }),
    });
    assert.equal(complete.status, 302);
    const location = new URL(complete.headers.get('location'));
    const token = location.searchParams.get('access_token');
    assert.ok(token);
    assert.equal(location.origin, 'http://127.0.0.1:8081');
    assert.equal(location.pathname, '/connect');

    const withState = await fetch(`${example.origin}/api/auth/sso`, {
      method: 'POST',
      redirect: 'manual',
      headers: { 'content-type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        redirect_uri: 'http://127.0.0.1:8081/connect',
        state: 'abc123',
      }),
    });
    assert.equal(new URL(withState.headers.get('location')).searchParams.get('state'), 'abc123');

    const entitlements = await getJson(example.origin, '/api/entitlements', token);
    assert.equal(entitlements.displayName, 'Shared Server');
    assert.equal(entitlements.features['workspace.create'].available, false);
    assert.equal(entitlements.features['agent.prompt'].available, true);

    const workspaces = await getJson(example.origin, '/api/workspaces', token);
    assert.deepEqual(workspaces, []);
  } finally {
    await example.dispose();
  }
});

async function getJson(origin, path, token) {
  const response = await fetch(`${origin}${path}`, {
    headers: token ? { authorization: `Bearer ${token}` } : {},
  });
  const body = await response.text();
  assert.equal(response.status, 200, body);
  return JSON.parse(body);
}
