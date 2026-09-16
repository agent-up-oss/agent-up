import assert from 'node:assert/strict';
import test from 'node:test';

import { resolveAction, runAction, submitCode } from '../dist/index.js';
import { isAwaitedRedirect } from '../dist/adapters/native.js';

function recordingPort(overrides = {}) {
  const calls = { opened: [], intercepted: [], copied: [] };
  return {
    calls,
    port: {
      canInterceptRedirect: overrides.canInterceptRedirect ?? true,
      async openUrl(url) {
        calls.opened.push(url);
      },
      async openInterceptingRedirect(url, redirectUri) {
        calls.intercepted.push({ url, redirectUri });
        return overrides.callback ?? null;
      },
      async copy(value) {
        calls.copied.push(value);
      },
      ...overrides.port,
    },
  };
}

function recordingApi() {
  const calls = { codes: [], callbacks: [] };
  return {
    calls,
    api: {
      async submitCode(code) {
        calls.codes.push(code);
      },
      async submitCallback(url) {
        calls.callbacks.push(url);
      },
    },
  };
}

test('a polling sign-in opens the link and sends nothing back', async () => {
  const { calls, port } = recordingPort();
  const { calls: apiCalls, api } = recordingApi();

  const outcome = await runAction(
    resolveAction({ url: 'https://cursor.com/login/abc', code: null, instructions: null, transport: 'poll' }),
    port,
    api,
  );

  assert.equal(outcome.kind, 'opened');
  assert.deepEqual(calls.opened, ['https://cursor.com/login/abc']);
  assert.equal(apiCalls.codes.length + apiCalls.callbacks.length, 0);
});

test('a pasted-code sign-in opens the link and then waits for the user', async () => {
  const { calls, port } = recordingPort();
  const { api } = recordingApi();

  const outcome = await runAction(
    resolveAction({
      url: 'https://claude.ai/oauth/authorize',
      code: null,
      instructions: null,
      transport: 'code',
      canSubmitCode: true,
    }),
    port,
    api,
  );

  assert.equal(outcome.kind, 'awaitingCode');
  assert.deepEqual(calls.opened, ['https://claude.ai/oauth/authorize']);
});

test('an intercepted redirect is handed straight back to the Server', async () => {
  const callback = 'http://localhost:1455/auth/callback?code=abc&state=xyz';
  const { calls, port } = recordingPort({ callback });
  const { calls: apiCalls, api } = recordingApi();

  const outcome = await runAction(
    resolveAction({
      url: 'https://auth.openai.com/oauth/authorize',
      code: null,
      instructions: null,
      transport: 'redirect',
      redirectUri: 'http://localhost:1455/auth/callback',
    }),
    port,
    api,
  );

  assert.equal(outcome.kind, 'callbackSubmitted');
  assert.deepEqual(apiCalls.callbacks, [callback]);
  assert.deepEqual(calls.intercepted, [
    { url: 'https://auth.openai.com/oauth/authorize', redirectUri: 'http://localhost:1455/auth/callback' },
  ]);
});

test('a redirect the user abandons reports that, and posts nothing', async () => {
  const { port } = recordingPort({ callback: null });
  const { calls: apiCalls, api } = recordingApi();

  const outcome = await runAction(
    resolveAction({
      url: 'https://auth.openai.com/oauth/authorize',
      code: null,
      instructions: null,
      transport: 'redirect',
      redirectUri: 'http://localhost:1455/auth/callback',
    }),
    port,
    api,
  );

  assert.equal(outcome.kind, 'abandoned');
  assert.deepEqual(apiCalls.callbacks, []);
});

// A browser cannot read a cross-origin popup's location, so it must not pretend to intercept.
// Opening the link is enough: the redirect reaches the agent CLI's listener directly whenever the
// browser is on the Server's host, which is the only case a browser can complete this at all.
test('a platform that cannot intercept just opens the link and posts nothing', async () => {
  const { calls, port } = recordingPort({ canInterceptRedirect: false });
  const { calls: apiCalls, api } = recordingApi();

  const outcome = await runAction(
    resolveAction({
      url: 'https://auth.openai.com/oauth/authorize',
      code: null,
      instructions: null,
      transport: 'redirect',
      redirectUri: 'http://localhost:1455/auth/callback',
    }),
    port,
    api,
  );

  assert.equal(outcome.kind, 'opened');
  assert.deepEqual(calls.opened, ['https://auth.openai.com/oauth/authorize']);
  assert.deepEqual(calls.intercepted, [], 'it must not attempt an interception it cannot perform');
  assert.deepEqual(apiCalls.callbacks, []);
});

test('waiting does nothing at all', async () => {
  const { calls, port } = recordingPort();
  const { api } = recordingApi();

  const outcome = await runAction(resolveAction(null), port, api);

  assert.equal(outcome.kind, 'nothingToDo');
  assert.deepEqual(calls.opened, []);
});

test('a submitted code is trimmed, and an empty one is refused without a request', async () => {
  const { calls, api } = recordingApi();

  assert.equal(await submitCode('  code-from-the-browser#state \n', api), true);
  assert.deepEqual(calls.codes, ['code-from-the-browser#state']);

  assert.equal(await submitCode('   ', api), false);
  assert.equal(calls.codes.length, 1, 'an empty code must not reach the Server');
});

test('the awaited redirect is recognised across both loopback spellings', () => {
  const expected = 'http://localhost:1455/auth/callback';

  assert.equal(isAwaitedRedirect('http://127.0.0.1:1455/auth/callback?code=abc', expected), true);
  assert.equal(isAwaitedRedirect('http://localhost:1455/auth/callback?code=abc', expected), true);
  assert.equal(isAwaitedRedirect('http://localhost:9999/auth/callback', expected), false, 'a different port');
  assert.equal(isAwaitedRedirect('http://localhost:1455/other', expected), false, 'a different path');
  assert.equal(isAwaitedRedirect('https://evil.example.com/auth/callback', expected), false, 'not loopback');
  assert.equal(isAwaitedRedirect('not a url', expected), false);
});
