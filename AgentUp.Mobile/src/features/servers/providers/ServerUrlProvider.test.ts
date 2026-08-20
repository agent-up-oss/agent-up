import assert from 'node:assert/strict';
import test from 'node:test';
import { normalizeServerUrl, probeServer } from './ServerUrlProvider';

test('normalizes HTTP and HTTPS base URLs', () => {
  assert.equal(normalizeServerUrl(' http://localhost:5000/ '), 'http://localhost:5000');
  assert.equal(normalizeServerUrl('https://agent-up.example/base/'), 'https://agent-up.example/base');
});

test('rejects unsupported schemes and credentials', () => {
  assert.throws(() => normalizeServerUrl('ftp://example.test'), /http or https/);
  assert.throws(() => normalizeServerUrl('https://user:pass@example.test'), /base URL/);
});

test('probes the workspace API', async () => {
  let requested = '';
  await probeServer('https://agent-up.example', (async (input: string | URL | Request) => {
    requested = String(input); return new Response('[]', { status: 200 });
  }) as typeof fetch);
  assert.equal(requested, 'https://agent-up.example/api/workspaces');
});
