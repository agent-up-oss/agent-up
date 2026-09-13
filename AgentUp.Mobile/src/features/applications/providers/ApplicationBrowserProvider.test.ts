import assert from 'node:assert/strict';
import test from 'node:test';
import {
  applicationHttpPort,
  applicationProxyUrl,
  issueApplicationProxyTicket,
} from './ApplicationBrowserProvider';

test('selects the first HTTP port without exposing a device-local URL', () => {
  const port = applicationHttpPort({ name: 'web', state: 'Running', allocatedPorts: [
    { allocatedPort: 5442, protocol: 'tcp' }, { allocatedPort: 6111, protocol: 'HTTP' },
  ] });
  assert.equal(port, 6111);
});

test('places the one-time ticket in the bootstrap query instead of the long-lived bearer token', () => {
  const url = applicationProxyUrl(
    { url: 'https://agent.example', accessToken: 'a+b/c=' },
    { ticket: 'deadbeef', bootstrapPath: '/apps/work%2Ftree/6111', expiresAt: '2026-09-13T12:00:00Z' },
  );
  assert.equal(url, 'https://agent.example/apps/work%2Ftree/6111?ticket=deadbeef');
  assert.equal(new URL(url).searchParams.has('access_token'), false);
  assert.equal(url.includes('a+b/c='), false);
});

test('asks the Server for a ticket bound to the workspace and allocated HTTP port', async () => {
  let requestedUrl = '';
  let authorization = '';
  let body = '';
  const request = (async (input: string | URL | Request, init?: RequestInit) => {
    requestedUrl = String(input);
    authorization = String((init?.headers as Record<string, string>).Authorization);
    body = String(init?.body);
    return Response.json({ ticket: 'abc123', bootstrapPath: '/apps/workspace%2Fid/8080', expiresAt: '2026-09-13T12:00:00Z' });
  }) as typeof fetch;

  const ticket = await issueApplicationProxyTicket(
    { url: 'https://agent.example', accessToken: 'secret' },
    'workspace/id',
    { name: 'web', state: 'Running', allocatedPorts: [{ allocatedPort: 8080, protocol: 'http' }] },
    request,
  );

  assert.equal(requestedUrl, 'https://agent.example/api/apps/tickets');
  assert.equal(authorization, 'Bearer secret');
  assert.equal(JSON.parse(body).workspaceId, 'workspace/id');
  assert.equal(JSON.parse(body).allocatedPort, 8080);
  assert.equal(ticket.ticket, 'abc123');
});

test('rejects applications without an HTTP display', async () => {
  await assert.rejects(
    issueApplicationProxyTicket(
      { url: 'https://agent.example' }, 'workspace',
      { name: 'database', state: 'Running', allocatedPorts: [{ allocatedPort: 5432, protocol: 'tcp' }] },
    ),
    /does not expose an HTTP port/,
  );
});

test('rejects proxy credentials over non-loopback HTTP', () => {
  assert.throws(
    () => applicationProxyUrl(
      { url: 'http://192.168.1.20:5000', accessToken: 'secret' },
      { ticket: 'abc', bootstrapPath: '/apps/workspace/8080', expiresAt: '2026-09-13T12:00:00Z' },
    ),
    /HTTPS is required/,
  );
  assert.doesNotThrow(
    () => applicationProxyUrl(
      { url: 'http://localhost:5000', accessToken: 'secret' },
      { ticket: 'abc', bootstrapPath: '/apps/workspace/8080', expiresAt: '2026-09-13T12:00:00Z' },
    ),
  );
});

test('rejects ticket requests over non-loopback HTTP before making a request', async () => {
  let called = false;
  const request = (async () => { called = true; return new Response(null, { status: 204 }); }) as typeof fetch;
  await assert.rejects(
    issueApplicationProxyTicket(
      { url: 'http://server.example:5000', accessToken: 'secret' },
      'workspace',
      { name: 'web', state: 'Running', allocatedPorts: [{ allocatedPort: 8080, protocol: 'http' }] },
      request,
    ),
    /HTTPS is required/,
  );
  assert.equal(called, false);
});

test('propagates caller cancellation to the ticket request', async () => {
  const caller = new AbortController();
  let requestAborted = false;
  const pendingFetch = (async (_url: string | URL | Request, init: RequestInit = {}) => {
    return await new Promise<Response>((_resolve, reject) =>
      init.signal?.addEventListener('abort', () => {
        requestAborted = true;
        reject(Object.assign(new Error('aborted'), { name: 'AbortError' }));
      }, { once: true }));
  }) as typeof fetch;

  const pending = issueApplicationProxyTicket(
    { url: 'https://agent.example', accessToken: 'secret' },
    'workspace',
    { name: 'web', state: 'Running', allocatedPorts: [{ allocatedPort: 8080, protocol: 'http' }] },
    pendingFetch,
    caller.signal,
  );
  caller.abort();
  await assert.rejects(pending, /request was cancelled/);
  assert.equal(requestAborted, true);
});
