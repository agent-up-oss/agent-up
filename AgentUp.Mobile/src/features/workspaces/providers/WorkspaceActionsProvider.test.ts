import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { Workspace } from '../models/Workspace';
import { createWorkspaceActions, type WorkspaceActionsApi } from './WorkspaceActionsProvider';
import type { WorkspaceRefresher } from './WorkspaceRefreshProvider';

function workspace(id: string): Workspace {
  return {
    id,
    displayName: id,
    repositoryPath: `/clones/${id}`,
    worktreePath: `/clones/${id}`,
    branch: 'main',
    commit: 'abc123',
    state: 'Stopped',
  };
}

function at(url: string): ServerSession {
  return { url };
}

// Stands in for the real refresher: the test drives which server is active, and the provider
// records every refresh it is asked to perform.
function fakeRefresher(active: { url: string | null }, refreshed: (string | null)[]): WorkspaceRefresher {
  return {
    isActive: server => (server?.url ?? null) === active.url,
    refresh: async server => { refreshed.push(server?.url ?? null); },
  };
}

function api(overrides: Partial<WorkspaceActionsApi> = {}): WorkspaceActionsApi {
  return {
    clone: async () => workspace('cloned'),
    start: async () => {},
    stop: async () => {},
    ...overrides,
  };
}

test('a clone that finishes while its server is active refreshes and selects the new workspace', async () => {
  const active = { url: 'http://a' as string | null };
  const refreshed: (string | null)[] = [];
  const selected: string[] = [];
  const actions = createWorkspaceActions(fakeRefresher(active, refreshed), api(), { onSelect: id => { selected.push(id); } });

  const cloned = await actions.clone(at('http://a'), { repository: 'https://example.test/acme/widgets.git', branch: 'main' });

  assert.equal(cloned.id, 'cloned');
  assert.deepEqual(refreshed, ['http://a']);
  assert.deepEqual(selected, ['cloned']);
});

test('a clone that finishes after the user switched servers neither refreshes nor selects', async () => {
  const active = { url: 'http://a' as string | null };
  const refreshed: (string | null)[] = [];
  const selected: string[] = [];
  let releaseClone: (created: Workspace) => void = () => {};
  const actions = createWorkspaceActions(
    fakeRefresher(active, refreshed),
    api({ clone: () => new Promise<Workspace>(resolve => { releaseClone = resolve; }) }),
    { onSelect: id => { selected.push(id); } },
  );

  const pending = actions.clone(at('http://a'), { repository: 'https://example.test/acme/widgets.git', branch: 'main' });

  // The user moves to another server while the clone is still running.
  active.url = 'http://b';

  releaseClone(workspace('cloned-on-a'));
  const cloned = await pending;

  assert.equal(cloned.id, 'cloned-on-a', 'the caller still learns the clone succeeded');
  assert.deepEqual(refreshed, [], 'server A must not be refreshed while server B is active');
  assert.deepEqual(selected, [], 'a workspace from server A must not be selected on server B');
});

test('a clone without a server is refused before any request', async () => {
  const active = { url: null as string | null };
  let requested = false;
  const actions = createWorkspaceActions(
    fakeRefresher(active, []),
    api({ clone: async () => { requested = true; return workspace('cloned'); } }),
    { onSelect: () => {} },
  );

  await assert.rejects(
    () => actions.clone(null, { repository: 'https://example.test/acme/widgets.git', branch: 'main' }),
    /Connect this client to an Agent-Up Server first\./,
  );
  assert.equal(requested, false);
});

test('start and stop refresh only while their server is still active', async () => {
  const active = { url: 'http://a' as string | null };
  const refreshed: (string | null)[] = [];
  const actions = createWorkspaceActions(fakeRefresher(active, refreshed), api(), { onSelect: () => {} });

  await actions.start(at('http://a'), 'ws-1');
  await actions.stop(at('http://a'), 'ws-1');
  assert.deepEqual(refreshed, ['http://a', 'http://a']);

  active.url = 'http://b';
  await actions.start(at('http://a'), 'ws-1');
  await actions.stop(at('http://a'), 'ws-1');
  assert.deepEqual(refreshed, ['http://a', 'http://a'], 'no further refresh for the server left behind');
});

test('start and stop without a server do nothing', async () => {
  const refreshed: (string | null)[] = [];
  let requested = false;
  const actions = createWorkspaceActions(
    fakeRefresher({ url: null }, refreshed),
    api({
      start: async () => { requested = true; },
      stop: async () => { requested = true; },
    }),
    { onSelect: () => {} },
  );

  await actions.start(null, 'ws-1');
  await actions.stop(null, 'ws-1');

  assert.equal(requested, false);
  assert.deepEqual(refreshed, []);
});
