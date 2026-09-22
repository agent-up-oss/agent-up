import assert from 'node:assert/strict';
import { test } from 'node:test';
import { disableCapabilityModule, enableCapabilityModule, listCapabilityModules } from './CapabilityModulesApiProvider';

type Recorded = { url: string; init: RequestInit };

function fakeFetch(status: number, body: string, recorded: Recorded[]): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

const moduleBody = JSON.stringify({
  id: 'dotnet',
  version: '1.0.0',
  displayName: '.NET',
  publisher: 'agent-up',
  kind: 'runtime',
  enabled: true,
  state: 'ready',
  canRun: true,
  messages: [],
});

test('listCapabilityModules requests the Server catalog', async () => {
  const recorded: Recorded[] = [];
  const modules = await listCapabilityModules(
    { url: 'http://localhost:5000' },
    fakeFetch(200, `[${moduleBody}]`, recorded),
  );

  assert.equal(modules[0]?.id, 'dotnet');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/capabilities');
});

test('enableCapabilityModule posts the package identity', async () => {
  const recorded: Recorded[] = [];
  const module = await enableCapabilityModule(
    { url: 'http://localhost:5000' },
    'dotnet',
    '1.0.0',
    fakeFetch(200, moduleBody, recorded),
  );

  assert.equal(module.enabled, true);
  assert.equal(recorded[0].url, 'http://localhost:5000/api/capabilities/enable');
  assert.equal(recorded[0].init.method, 'POST');
  assert.match(String(recorded[0].init.body), /"id":"dotnet"/);
});

test('disableCapabilityModule posts the escaped package id', async () => {
  const recorded: Recorded[] = [];
  await disableCapabilityModule(
    { url: 'http://localhost:5000' },
    'dot net',
    fakeFetch(200, moduleBody, recorded),
  );

  assert.equal(recorded[0].url, 'http://localhost:5000/api/capabilities/disable/dot%20net');
});
