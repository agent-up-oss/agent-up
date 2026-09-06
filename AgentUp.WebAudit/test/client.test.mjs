import assert from 'node:assert/strict';
import test from 'node:test';
import { createAgentUpAudit } from '../dist/index.js';

test('records a frontend event with managed workspace and application identity', async () => {
  let captured;
  const audit = createAgentUpAudit({
    workspaceId: 'ws-1', application: 'web',
    fetch: async (url, init) => { captured = { url, init }; return new Response(null, { status: 204 }); },
  });

  await audit.record({ action: 'server_connection_failed', outcome: 'failure', details: { message: 'Load failed' } });

  assert.equal(captured.url, 'http://127.0.0.1:5000/api/audit/record');
  assert.deepEqual(JSON.parse(captured.init.body), {
    kind: 'frontend', source: 'web', action: 'server_connection_failed', outcome: 'failure',
    workspaceId: 'ws-1', details: { message: 'Load failed', application: 'web' },
  });
});

test('reports endpoint status failures', async () => {
  const audit = createAgentUpAudit({ fetch: async () => new Response(null, { status: 403 }) });
  await assert.rejects(() => audit.record({ action: 'test' }), /returned 403/);
});
