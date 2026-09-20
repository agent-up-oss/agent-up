import assert from 'node:assert/strict';
import { test } from 'node:test';
import { parseWorkspaceEventFrames, streamWorkspaceEvents } from './WorkspaceEventsApiProvider';

test('parses fragmented workspace SSE frames without losing the tail', () => {
  const first = parseWorkspaceEventFrames('data: {"workspaceId":"ws-1","state":"Starting",\n');
  assert.equal(first.events.length, 0);
  const second = parseWorkspaceEventFrames(first.rest + 'data: "applications":[{"name":"Web","state":"Starting"}]}\n\npartial');
  assert.equal(second.events[0]?.workspaceId, 'ws-1');
  assert.equal(second.events[0]?.state, 'Starting');
  assert.equal(second.events[0]?.applications[0]?.name, 'Web');
  assert.equal(second.rest, 'partial');
});

test('reads healthState and ignores keepalives and malformed frames', () => {
  const parsed = parseWorkspaceEventFrames(
    ': keepalive\n\ndata: nope\n\ndata: {"workspaceId":"ws-1","state":"Running","healthState":"Checking","applications":[]}\n\n',
  );
  assert.equal(parsed.events.length, 1);
  assert.equal(parsed.events[0]?.healthState, 'Checking');
});

test('drops an incomplete frame that exceeds one megabyte', () => {
  const parsed = parseWorkspaceEventFrames(`data: ${'x'.repeat(1_048_577)}`);
  assert.equal(parsed.rest, '');
});

test('stream sends authentication and flushes the final frame', async () => {
  let url = '';
  let authorization = '';
  const request = async (input: string | URL | Request, init?: RequestInit) => {
    url = String(input);
    authorization = new Headers(init?.headers).get('Authorization') ?? '';
    const bytes = new TextEncoder().encode('data: {"workspaceId":"ws-1","state":"Running","applications":[]}');
    return new Response(new ReadableStream({ start(controller) { controller.enqueue(bytes); controller.close(); } }), { status: 200 });
  };
  const received: string[] = [];
  await streamWorkspaceEvents(
    { url: 'https://server.example', accessToken: 'secret' },
    event => received.push(event.state),
    new AbortController().signal,
    request as typeof fetch,
  );
  assert.equal(url, 'https://server.example/api/workspaces/events');
  assert.equal(authorization, 'Bearer secret');
  assert.deepEqual(received, ['Running']);
});

test('stream rejects a remote HTTP URL before attaching the bearer token', async () => {
  let called = false;
  const request = async () => {
    called = true;
    return new Response(null, { status: 204 });
  };
  await assert.rejects(
    () => streamWorkspaceEvents(
      { url: 'http://192.168.1.10:5000', accessToken: 'secret' },
      () => undefined,
      new AbortController().signal,
      request as typeof fetch,
    ),
    /HTTPS is required/,
  );
  assert.equal(called, false);
});
