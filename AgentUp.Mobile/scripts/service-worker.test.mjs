import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import test from 'node:test';

test('an installed release prevents a later service-worker deployment from replacing its payload', async () => {
  let install;
  let networkRequests = 0;
  let skipWaitingCalls = 0;
  const marker = { match: async () => new Response('agent-up-release-235-abcdef0') };
  const context = {
    Response,
    URL,
    caches: { open: async name => name === 'agent-up-release-marker' ? marker : assert.fail(`opened ${name}`) },
    fetch: async () => { networkRequests += 1; },
    self: {
      addEventListener: (type, handler) => { if (type === 'install') install = handler; },
      clients: { claim: async () => {} },
      location: { origin: 'https://agent-up.example' },
      skipWaiting: async () => { skipWaitingCalls += 1; },
    },
  };
  runInNewContext(readFileSync(new URL('../public/sw.js', import.meta.url), 'utf8'), context);

  let installation;
  install({ waitUntil: promise => { installation = promise; } });
  await installation;

  assert.equal(networkRequests, 0);
  assert.equal(skipWaitingCalls, 1);
});

test('bootstrap installation strips redirect metadata from cached assets', async () => {
  let install;
  let stored;
  const marker = {
    match: async () => undefined,
    put: async () => {},
  };
  const releaseCache = { put: async (path, response) => { stored = { path, response }; } };
  const redirected = new Response('mobile app', { headers: { 'Content-Type': 'text/html' } });
  Object.defineProperty(redirected, 'redirected', { value: true });
  const context = {
    Response,
    URL,
    caches: { open: async name => name === 'agent-up-release-marker' ? marker : releaseCache, delete: async () => true },
    fetch: async path => path === '/bootstrap-manifest.json'
      ? new Response(JSON.stringify({ cacheName: 'agent-up-release-bootstrap-main-source', files: ['/index.html'] }), { headers: { 'Content-Type': 'application/json' } })
      : redirected,
    self: {
      addEventListener: (type, handler) => { if (type === 'install') install = handler; },
      clients: { claim: async () => {} },
      location: { origin: 'https://agent-up.example' },
      skipWaiting: async () => {},
    },
  };
  runInNewContext(readFileSync(new URL('../public/sw.js', import.meta.url), 'utf8'), context);

  let installation;
  install({ waitUntil: promise => { installation = promise; } });
  await installation;

  assert.equal(stored.path, '/index.html');
  assert.equal(stored.response.redirected, false);
  assert.equal(await stored.response.text(), 'mobile app');
});

test('navigation strips redirect metadata from an existing cached response', async () => {
  let fetchHandler;
  const redirected = new Response('mobile app', { headers: { 'Content-Type': 'text/html' } });
  Object.defineProperty(redirected, 'redirected', { value: true });
  const marker = { match: async () => new Response('agent-up-release-bootstrap-main-source') };
  const releaseCache = { match: async path => path === '/index.html' ? redirected : undefined };
  const context = {
    Response,
    URL,
    caches: { open: async name => name === 'agent-up-release-marker' ? marker : releaseCache },
    fetch: async () => assert.fail('navigation should be served from cache'),
    self: {
      addEventListener: (type, handler) => { if (type === 'fetch') fetchHandler = handler; },
      clients: { claim: async () => {} },
      location: { origin: 'https://agent-up.example' },
      skipWaiting: async () => {},
    },
  };
  runInNewContext(readFileSync(new URL('../public/sw.js', import.meta.url), 'utf8'), context);

  let responsePromise;
  fetchHandler({
    request: { method: 'GET', mode: 'navigate', url: 'https://agent-up.example/' },
    respondWith: promise => { responsePromise = promise; },
  });
  const response = await responsePromise;

  assert.equal(response.redirected, false);
  assert.equal(await response.text(), 'mobile app');
});
