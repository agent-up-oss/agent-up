import assert from 'node:assert/strict';
import test from 'node:test';

import { hasExpired, normalizeTransport, resolveAction } from '../dist/index.js';

test('no challenge yet means there is nothing for the client to do', () => {
  assert.equal(resolveAction(null).kind, 'wait');
  assert.equal(resolveAction(undefined).kind, 'wait');
  assert.equal(resolveAction({ url: null, code: null, instructions: null }).kind, 'wait');
});

test('a polling sign-in only asks the client to open the link', () => {
  const action = resolveAction({
    url: 'https://cursor.com/loginDeepControl?uuid=abc',
    code: null,
    instructions: 'Open this link and sign in with your subscription.',
    transport: 'poll',
  });

  assert.equal(action.kind, 'open');
  assert.equal(action.url, 'https://cursor.com/loginDeepControl?uuid=abc');
});

test('a device code is shown alongside the link and never sent back', () => {
  const action = resolveAction({
    url: 'https://auth.openai.com/device',
    code: 'ABCD-EFGH',
    instructions: 'Open this link, then enter the code shown here.',
    transport: 'code',
  });

  assert.equal(action.kind, 'openWithCode');
  assert.equal(action.code, 'ABCD-EFGH');
});

test('a pasted-code sign-in collects a code, and says whether the agent is ready for it', () => {
  const challenge = {
    url: 'https://claude.ai/oauth/authorize?code=true',
    code: null,
    instructions: 'Open this link, sign in, then paste the code it gives you back here.',
    transport: 'code',
    canSubmitCode: false,
  };

  assert.deepEqual(
    { kind: resolveAction(challenge).kind, ready: resolveAction(challenge).ready },
    { kind: 'collectCode', ready: false },
    'the agent has not written its prompt yet',
  );

  assert.equal(resolveAction({ ...challenge, canSubmitCode: true }).ready, true);
});

test('a redirect sign-in asks the client to watch for the loopback address', () => {
  const action = resolveAction({
    url: 'https://auth.openai.com/oauth/authorize?redirect_uri=http%3A%2F%2Flocalhost%3A1455%2Fauth%2Fcallback',
    code: null,
    instructions: 'Open this link and sign in.',
    transport: 'redirect',
    redirectUri: 'http://localhost:1455/auth/callback',
  });

  assert.equal(action.kind, 'interceptRedirect');
  assert.equal(action.redirectUri, 'http://localhost:1455/auth/callback');
});

test('a redirect with no loopback address degrades to a plain open rather than watching nothing', () => {
  const action = resolveAction({
    url: 'https://auth.openai.com/oauth/authorize',
    code: null,
    instructions: null,
    transport: 'redirect',
    redirectUri: null,
  });

  assert.equal(action.kind, 'open');
});

test('an unknown transport behaves like the least demanding one', () => {
  const action = resolveAction({
    url: 'https://example.com/login',
    code: null,
    instructions: null,
  });

  assert.equal(action.kind, 'open');
  assert.equal(normalizeTransport(undefined), 'unknown');
  assert.equal(normalizeTransport('REDIRECT'), 'redirect');
  assert.equal(normalizeTransport('carrier-pigeon'), 'unknown');
});

test('expiry is reported only when it has actually passed', () => {
  const now = new Date('2026-01-01T12:00:00Z');
  assert.equal(hasExpired({ url: 'x', code: null, instructions: null }, now), false);
  assert.equal(hasExpired({ url: 'x', code: null, instructions: null, expiresAt: '2026-01-01T12:05:00Z' }, now), false);
  assert.equal(hasExpired({ url: 'x', code: null, instructions: null, expiresAt: '2026-01-01T11:59:00Z' }, now), true);
  assert.equal(hasExpired({ url: 'x', code: null, instructions: null, expiresAt: 'not a date' }, now), false);
});
