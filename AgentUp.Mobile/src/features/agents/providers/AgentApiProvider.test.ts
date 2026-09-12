import assert from 'node:assert/strict';
import test from 'node:test';
import { authenticateAgent, parseSseFrames, scheduleAgent, streamAgentEvents } from './AgentApiProvider';

test('parses fragmented and multiline SSE events without losing the tail', () => {
  const first = parseSseFrames('id: 1\nevent: state\ndata: {"sequence":1,\n');
  assert.equal(first.events.length, 0);
  const second = parseSseFrames(first.rest + 'data: "type":"state","payload":{},"timestamp":"now"}\n\npartial');
  assert.equal(second.events[0]?.sequence, 1);
  assert.equal(second.rest, 'partial');
});

test('ignores keepalives and malformed data frames', () => {
  const parsed = parseSseFrames(': keepalive\n\ndata: nope\n\n');
  assert.deepEqual(parsed.events, []);
});

test('drops an incomplete frame that exceeds one megabyte', () => {
  const parsed = parseSseFrames(`data: ${'x'.repeat(1_048_577)}`);
  assert.equal(parsed.rest, '');
});

test('stream sends authentication, resumes after cursor, and flushes final frame', async () => {
  let url = ''; let authorization = '';
  const request = async (input: string | URL | Request, init?: RequestInit) => {
    url = String(input); authorization = new Headers(init?.headers).get('Authorization') ?? '';
    const bytes = new TextEncoder().encode('id: 8\nevent: state\ndata: {"sequence":8,"type":"state","payload":{},"timestamp":"now"}');
    return new Response(new ReadableStream({ start(controller) { controller.enqueue(bytes); controller.close(); } }), { status: 200 });
  };
  const received: number[] = [];
  await streamAgentEvents({ url: 'https://server.example', accessToken: 'secret' }, 'ws /1', 7, event => received.push(event.sequence), new AbortController().signal, request as typeof fetch);
  assert.equal(url, 'https://server.example/api/workspaces/ws%20%2F1/agent/events?after=7');
  assert.equal(authorization, 'Bearer secret');
  assert.deepEqual(received, [8]);
});

test('stream rejects a remote HTTP URL before attaching the bearer token', async () => {
  let called = false;
  const request = async () => {
    called = true;
    return new Response(null, { status: 204 });
  };
  await assert.rejects(
    () => streamAgentEvents(
      { url: 'http://192.168.1.10:5000', accessToken: 'secret' },
      'ws',
      0,
      () => undefined,
      new AbortController().signal,
      request as typeof fetch,
    ),
    /HTTPS is required/,
  );
  assert.equal(called, false);
});

test('schedule and authenticate use workspace-scoped authenticated JSON requests', async () => {
  const calls: { url: string; init?: RequestInit }[] = [];
  const request = async (input: string | URL | Request, init?: RequestInit) => {
    calls.push({ url: String(input), init });
    return new Response(init?.method === 'POST' && calls.length === 1
      ? JSON.stringify({ workspaceId: 'ws', agent: 'Codex', state: 'ready', sessionId: 's', error: null, agents: [], authMethods: [] })
      : null, { status: calls.length === 1 ? 200 : 204, headers: { 'Content-Type': 'application/json' } });
  };
  const server = { url: 'https://server.example', accessToken: 'token' };
  await scheduleAgent(server, 'ws /1', 'Codex', request as typeof fetch);
  await authenticateAgent(server, 'ws /1', 'chatgpt', request as typeof fetch);
  assert.deepEqual(calls.map(call => call.url), [
    'https://server.example/api/workspaces/ws%20%2F1/agent',
    'https://server.example/api/workspaces/ws%20%2F1/agent/authenticate',
  ]);
  assert.equal(new Headers(calls[0]?.init?.headers).get('Authorization'), 'Bearer token');
  assert.equal(calls[0]?.init?.body, '{"agent":"Codex"}');
  assert.equal(calls[1]?.init?.body, '{"methodId":"chatgpt"}');
});
