import assert from 'node:assert/strict';
import { test } from 'node:test';
import { saveTaskSourceToken } from './TaskSourceProvider';

type Recorded = { url: string; init: RequestInit };

function fakeFetch(status: number, body: string, recorded: Recorded[]): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

test('saveTaskSourceToken puts the member, host URL and token and does not keep the token in the answer', async () => {
  const recorded: Recorded[] = [];
  const source = await saveTaskSourceToken(
    { url: 'http://localhost:5000' },
    'frank',
    'forgejo',
    'https://git.example.test',
    'secret-token',
    fakeFetch(200, JSON.stringify({
      identities: [{
        workspaceId: 'frank',
        displayName: 'Frank',
        initials: 'FR',
        moduleId: 'forgejo',
        connected: true,
        detail: 'Reading.',
        endpointUrl: 'https://git.example.test',
      }],
    }), recorded),
  );

  assert.equal(recorded[0].url, 'http://localhost:5000/api/task-source');
  assert.equal(recorded[0].init.method, 'PUT');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), {
    workspaceId: 'frank',
    moduleId: 'forgejo',
    endpointUrl: 'https://git.example.test',
    token: 'secret-token',
  });
  assert.equal(source?.identities[0]?.detail.includes('secret-token'), false);
  assert.equal(source?.identities[0]?.endpointUrl, 'https://git.example.test');
});
