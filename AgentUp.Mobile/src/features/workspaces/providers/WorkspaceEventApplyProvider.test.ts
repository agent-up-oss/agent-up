import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { Workspace } from '../models/Workspace';
import { applyWorkspaceEvent } from './WorkspaceEventApplyProvider';

function workspace(id: string, state = 'Stopped'): Workspace {
  return {
    id,
    displayName: id,
    repositoryPath: `/clones/${id}`,
    worktreePath: `/clones/${id}`,
    branch: 'main',
    commit: 'abc123',
    state,
    applications: [{ name: 'Web', state: 'Stopped' }],
  };
}

test('applies lifecycle and health onto an existing workspace without dropping omitted apps', () => {
  const current = [workspace('ws-1', 'Starting')];

  const next = applyWorkspaceEvent(current, {
    workspaceId: 'ws-1',
    state: 'Running',
    healthState: 'Checking',
    applications: [{ name: 'Web', state: 'Checking' }, { name: 'API', state: 'Starting' }],
  });

  assert.equal(next.applied, true);
  assert.equal(next.workspaces[0]?.state, 'Running');
  assert.equal(next.workspaces[0]?.healthState, 'Checking');
  assert.deepEqual(next.workspaces[0]?.applications, [
    { name: 'Web', state: 'Checking' },
    { name: 'API', state: 'Starting' },
  ]);
});

test('clears previous health when the event omits healthState', () => {
  const current = [{ ...workspace('ws-1', 'Running'), healthState: 'Healthy' }];

  const next = applyWorkspaceEvent(current, {
    workspaceId: 'ws-1',
    state: 'Stopping',
    applications: [{ name: 'Web', state: 'Stopping' }],
  });

  assert.equal(next.workspaces[0]?.healthState, undefined);
  assert.equal(next.workspaces[0]?.state, 'Stopping');
});

test('does not apply an event for a workspace the client has not listed yet', () => {
  const current = [workspace('ws-1')];
  const next = applyWorkspaceEvent(current, {
    workspaceId: 'ws-2',
    state: 'Starting',
    applications: [],
  });

  assert.equal(next.applied, false);
  assert.equal(next.workspaces, current);
});

test('removes a workspace when the Server publishes Removed', () => {
  const current = [workspace('ws-1'), workspace('ws-2')];
  const next = applyWorkspaceEvent(current, {
    workspaceId: 'ws-1',
    state: 'Removed',
    applications: [],
  });

  assert.equal(next.applied, true);
  assert.deepEqual(next.workspaces.map(entry => entry.id), ['ws-2']);
});
