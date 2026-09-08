import assert from 'node:assert/strict';
import { test } from 'node:test';
import { requestServerJson, ServerRequestError } from './ServerRequestProvider';

// Mimics fetch: it resolves once headers exist, and its body stream fails when the request signal
// aborts. A server that stalls the body must therefore still hit the request timeout.
function stalledBodyFetch(): typeof fetch {
  return (async (_url: string | URL | Request, init: RequestInit = {}) => {
    const stream = new ReadableStream({
      start(controller) {
        init.signal?.addEventListener('abort', () =>
          controller.error(Object.assign(new Error('aborted'), { name: 'AbortError' })));
      },
    });
    return new Response(stream, { status: 200, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

test('a response that stalls its body still times out', async () => {
  await assert.rejects(
    () => requestServerJson('http://localhost:5000', '/api/workspaces', { method: 'GET' }, 25, stalledBodyFetch()),
    /did not respond in time/,
  );
});

test('a body that arrives within the timeout is returned', async () => {
  const slowBodyFetch = (async () => {
    const stream = new ReadableStream({
      async start(controller) {
        await new Promise(resolve => setTimeout(resolve, 10));
        controller.enqueue(new TextEncoder().encode('{"ok":true}'));
        controller.close();
      },
    });
    return new Response(stream, { status: 200 });
  }) as unknown as typeof fetch;

  const body = await requestServerJson<{ ok: boolean }>(
    'http://localhost:5000', '/api/workspaces', { method: 'GET' }, 2000, slowBodyFetch);

  assert.deepEqual(body, { ok: true });
});

test('a 204 response reads as no content', async () => {
  const noContentFetch = (async () => new Response(null, { status: 204 })) as unknown as typeof fetch;

  assert.equal(
    await requestServerJson('http://localhost:5000', '/api/workspaces/a/start', { method: 'POST' }, 2000, noContentFetch),
    null,
  );
});

test('an error response surfaces the problem detail and its status', async () => {
  const failingFetch = (async () =>
    new Response(JSON.stringify({ detail: 'Branch must be a valid Git branch name.' }), { status: 400 })
  ) as unknown as typeof fetch;

  const error = await requestServerJson('http://localhost:5000', '/api/source-clones', {}, 2000, failingFetch)
    .then(() => null, (cause: unknown) => cause);

  assert.ok(error instanceof ServerRequestError);
  assert.equal(error.status, 400);
  assert.equal(error.message, 'Branch must be a valid Git branch name.');
});

test('an unreachable server is reported with its origin', async () => {
  const refusingFetch = (async () => { throw new TypeError('fetch failed'); }) as unknown as typeof fetch;

  await assert.rejects(
    () => requestServerJson('http://localhost:5000', '/api/workspaces', {}, 2000, refusingFetch),
    /Could not reach http:\/\/localhost:5000/,
  );
});
