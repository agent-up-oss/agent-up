import assert from 'node:assert/strict';
import test from 'node:test';
import { ensureCredentialTransportAllowed, getAuthenticationStatus, login } from './AuthenticationProvider';

test('reads whether authentication is required', async () => {
  const result = await getAuthenticationStatus('http://server', (async () =>
    new Response(JSON.stringify({ authenticationRequired: false }))) as typeof fetch);
  assert.equal(result.authenticationRequired, false);
});

test('login posts the password and returns a token', async () => {
  let body = '';
  const result = await login('http://localhost:5000', 'secret', (async (_url, init) => {
    body = String(init?.body);
    return new Response(JSON.stringify({ authenticationRequired: true, accessToken: 'token' }));
  }) as typeof fetch);
  assert.deepEqual(JSON.parse(body), { password: 'secret' });
  assert.equal(result.accessToken, 'token');
});

test('login rejects remote http urls', () => {
  assert.throws(
    () => ensureCredentialTransportAllowed('http://192.168.1.10:5000'),
    /HTTPS is required/,
  );
});

test('login allows loopback http urls', () => {
  assert.doesNotThrow(() => ensureCredentialTransportAllowed('http://localhost:5000'));
});

test('login allows remote https urls', () => {
  assert.doesNotThrow(() => ensureCredentialTransportAllowed('https://agent-up.example.com'));
});

test('login disables redirects for credential-bearing requests', async () => {
  let redirect: RequestRedirect | undefined;
  await login('http://localhost:5000', 'secret', (async (_url, init) => {
    redirect = init?.redirect;
    return new Response(JSON.stringify({ authenticationRequired: true, accessToken: 'token' }));
  }) as typeof fetch);
  assert.equal(redirect, 'error');
});
