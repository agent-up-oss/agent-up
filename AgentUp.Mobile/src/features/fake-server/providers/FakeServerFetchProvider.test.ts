import assert from 'node:assert/strict';
import test from 'node:test';
import { loadFakeServerDefinition } from './FakeServerDefinitionProvider';
import { createFakeServerFetch } from './FakeServerFetchProvider';
import { FakeBackendService } from '../services/FakeBackendService';

test('intercepts the Demo URL and leaves other hosts alone', async () => {
  const service = new FakeBackendService(loadFakeServerDefinition());
  let forwarded = 0;
  const realFetch = (async () => {
    forwarded += 1;
    return new Response(null, { status: 204 });
  }) as typeof fetch;
  const request = createFakeServerFetch(service, realFetch);

  const workspaces = await request('http://127.0.0.1:9/api/workspaces');
  const passthrough = await request('http://127.0.0.1:5000/api/workspaces');

  assert.equal(workspaces.status, 200);
  assert.equal((await workspaces.json())[0].id, 'harbor-shop');
  assert.equal(passthrough.status, 204);
  assert.equal(forwarded, 1);
});

test('streams workspace events for the Demo URL', async () => {
  const service = new FakeBackendService(loadFakeServerDefinition());
  const request = createFakeServerFetch(service, (async () => new Response(null, { status: 204 })) as typeof fetch);
  const response = await request('http://127.0.0.1:9/api/workspaces/events');
  const reader = response.body!.getReader();
  const first = await reader.read();
  await reader.cancel();
  assert.equal(response.headers.get('Content-Type'), 'text/event-stream');
  assert.match(new TextDecoder().decode(first.value), /harbor-shop/);
});
