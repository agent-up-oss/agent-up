import assert from 'node:assert/strict';
import test from 'node:test';
import { recordServerConnectionAudit } from './MobileAuditProvider';

test('records successful server connections against the selected local server', async () => {
  let body = '';
  let requested = '';
  await recordServerConnectionAudit('http://localhost:5000', 'success', undefined,
    (async (input: string | URL | Request, init?: RequestInit) => {
      requested = String(input);
      body = String(init?.body);
      return new Response(null, { status: 204 });
    }) as typeof fetch,
    'http://localhost:5001/api/audit/record');

  assert.equal(requested, 'http://localhost:5001/api/audit/record');
  const event = JSON.parse(body);
  assert.equal(event.action, 'server_connection_succeeded');
  assert.equal(event.details.application, 'Mobile');
});

test('audit delivery failures never replace the connection result', async () => {
  await assert.doesNotReject(() => recordServerConnectionAudit(
    'http://localhost:5000', 'failure', 'Load failed',
    (async () => { throw new TypeError('Load failed'); }) as typeof fetch));
});
