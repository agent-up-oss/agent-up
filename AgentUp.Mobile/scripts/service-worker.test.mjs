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
