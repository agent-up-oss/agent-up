import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { Workspace } from '../models/Workspace';
import { createWorkspaceRefresh, type WorkspaceRefreshSink } from './WorkspaceRefreshProvider';

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

type Recorded = {
  sink: WorkspaceRefreshSink;
  applied: string[][];
  errors: string[];
  loading: boolean[];
  disconnects: number;
};

function recorder(): Recorded {
  const applied: string[][] = [];
  const errors: string[] = [];
  const loading: boolean[] = [];
  let disconnects = 0;
  const sink: WorkspaceRefreshSink = {
    onLoading: value => { loading.push(value); },
    onWorkspaces: value => { applied.push(value.map(w => w.id)); },
    onError: message => { errors.push(message); },
    onDisconnected: () => { disconnects++; },
  };
  return {
    sink,
    applied,
    errors,
    loading,
    get disconnects() { return disconnects; },
  } as Recorded;
}

test('a slow response for the previous server does not replace the current one', async () => {
  const recorded = recorder();
  let releaseSlow: (workspaces: Workspace[]) => void = () => {};
  const list = (serverUrl: string) =>
    serverUrl.endsWith('slow')
      ? new Promise<Workspace[]>(resolve => { releaseSlow = resolve; })
      : Promise.resolve([workspace('from-fast')]);
  const refresh = createWorkspaceRefresh(recorded.sink, list);

  const slow = refresh('http://slow');
  await refresh('http://fast');

  releaseSlow([workspace('from-slow')]);
  await slow;

  assert.deepEqual(recorded.applied, [['from-fast']]);
  assert.deepEqual(recorded.loading.at(-1), false);
});

test('a failure from the previous server does not replace the current one', async () => {
  const recorded = recorder();
  let failSlow: (cause: Error) => void = () => {};
  const list = (serverUrl: string) =>
    serverUrl.endsWith('slow')
      ? new Promise<Workspace[]>((_resolve, reject) => { failSlow = reject; })
      : Promise.resolve([workspace('from-fast')]);
  const refresh = createWorkspaceRefresh(recorded.sink, list);

  const slow = refresh('http://slow');
  await refresh('http://fast');

  failSlow(new Error('Could not reach http://slow.'));
  await slow;

  assert.deepEqual(recorded.errors, []);
  assert.deepEqual(recorded.applied, [['from-fast']]);
});

test('a response for the current server is applied', async () => {
  const recorded = recorder();
  const refresh = createWorkspaceRefresh(recorded.sink, () => Promise.resolve([workspace('ws-1')]));

  await refresh('http://localhost:5000');

  assert.deepEqual(recorded.applied, [['ws-1']]);
  assert.deepEqual(recorded.loading, [true, false]);
});

test('a failure for the current server is reported', async () => {
  const recorded = recorder();
  const refresh = createWorkspaceRefresh(recorded.sink, () => Promise.reject(new Error('Connection refused')));

  await refresh('http://localhost:5000');

  assert.deepEqual(recorded.errors, ['Connection refused']);
  assert.deepEqual(recorded.loading, [true, false]);
});

test('refreshing without a server disconnects instead of loading', async () => {
  const recorded = recorder();
  const refresh = createWorkspaceRefresh(recorded.sink, () => {
    throw new Error('the server must not be queried when none is selected');
  });

  await refresh(null);

  assert.equal(recorded.disconnects, 1);
  assert.deepEqual(recorded.loading, [false]);
});

test('a pending response is dropped once the server is deselected', async () => {
  const recorded = recorder();
  let release: (workspaces: Workspace[]) => void = () => {};
  const refresh = createWorkspaceRefresh(
    recorded.sink,
    () => new Promise<Workspace[]>(resolve => { release = resolve; }),
  );

  const pending = refresh('http://localhost:5000');
  await refresh(null);

  release([workspace('ws-1')]);
  await pending;

  assert.deepEqual(recorded.applied, []);
  assert.equal(recorded.disconnects, 1);
});
