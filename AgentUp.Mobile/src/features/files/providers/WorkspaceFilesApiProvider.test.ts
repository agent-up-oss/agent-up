import assert from 'node:assert/strict';
import { test } from 'node:test';
import { getDirectory, getFile, saveFile } from './WorkspaceFilesApiProvider';

type Recorded = { url: string; init: RequestInit };

const server = { url: 'http://localhost:5000' };

function fakeFetch(status: number, body: string, recorded: Recorded[] = []): typeof fetch {
  return (async (url: string | URL | Request, init: RequestInit = {}) => {
    recorded.push({ url: String(url), init });
    return new Response(body, { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

test('getDirectory asks for one folder of the workspace', async () => {
  const recorded: Recorded[] = [];
  const body = JSON.stringify({ workspaceId: 'ws 1', path: 'apps', entries: [], truncated: false });

  const listing = await getDirectory(server, 'ws 1', 'apps', fakeFetch(200, body, recorded));

  assert.equal(listing?.path, 'apps');
  assert.equal(recorded[0].url, 'http://localhost:5000/api/workspaces/ws%201/files/directory?path=apps');
});

test('getFile returns null when the file is gone', async () => {
  assert.equal(await getFile(server, 'ws', 'gone.md', fakeFetch(404, '')), null);
});

test('saveFile puts the content with the version it was edited from', async () => {
  const recorded: Recorded[] = [];
  const saved = JSON.stringify({ path: 'README.md', size: 4, version: 'v2', status: 'Modified', isBinary: false, isTooLarge: false, content: 'new\n' });

  const result = await saveFile(server, 'ws', 'README.md', 'new\n', 'v1', fakeFetch(200, saved, recorded));

  assert.equal(result.ok, true);
  assert.equal(recorded[0].init.method, 'PUT');
  assert.deepEqual(JSON.parse(String(recorded[0].init.body)), { path: 'README.md', content: 'new\n', version: 'v1' });
});

test('a stale save is a conflict and a busy agent is not', async () => {
  const stale = await saveFile(server, 'ws', 'README.md', 'x', 'v1',
    fakeFetch(409, JSON.stringify({ title: 'File changed on the Server', detail: "'README.md' changed on the Server since it was opened." })));
  const busy = await saveFile(server, 'ws', 'README.md', 'x', 'v1',
    fakeFetch(409, JSON.stringify({ title: 'Workspace agent is working', detail: 'The workspace agent is working.' })));

  assert.deepEqual(stale.ok ? null : stale.conflict, true);
  assert.deepEqual(busy.ok ? null : busy.conflict, false);
  assert.equal(busy.ok ? '' : busy.error, 'The workspace agent is working.');
});
