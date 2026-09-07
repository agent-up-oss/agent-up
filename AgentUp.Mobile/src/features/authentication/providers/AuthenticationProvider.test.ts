import assert from 'node:assert/strict';
import test from 'node:test';
import { getAuthenticationStatus, login } from './AuthenticationProvider';

test('reads whether authentication is required', async () => {
  const result = await getAuthenticationStatus('http://server', (async () =>
    new Response(JSON.stringify({ authenticationRequired: false }))) as typeof fetch);
  assert.equal(result.authenticationRequired, false);
});

test('login posts the password and returns a token', async () => {
  let body = '';
  const result = await login('http://server', 'secret', (async (_url, init) => {
    body = String(init?.body);
    return new Response(JSON.stringify({ authenticationRequired: true, accessToken: 'token' }));
  }) as typeof fetch);
  assert.deepEqual(JSON.parse(body), { password: 'secret' });
  assert.equal(result.accessToken, 'token');
});
