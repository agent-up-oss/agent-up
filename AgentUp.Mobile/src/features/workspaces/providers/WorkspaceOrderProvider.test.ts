import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { Workspace } from '../models/Workspace';
import { orderWorkspaces } from './WorkspaceOrderProvider';

function workspace(id: string, state: string, derived = false): Workspace {
  return {
    id,
    displayName: id,
    repositoryPath: `/clones/${id}`,
    worktreePath: `/clones/${id}`,
    branch: derived ? 'HEAD' : 'main',
    commit: 'abc123',
    state,
    derived,
    sourceRevision: derived ? 'topic' : null,
  };
}

test('own workspaces list before derived review workspaces', () => {
  const ordered = orderWorkspaces([
    workspace('review', 'Running', true),
    workspace('own', 'Stopped'),
  ]);
  assert.deepEqual(ordered.map(item => item.id), ['own', 'review']);
});

test('running own workspaces still beat stopped own workspaces, and beat derived peers', () => {
  const ordered = orderWorkspaces([
    workspace('zeta-review', 'Running', true),
    workspace('alpha-own', 'Stopped'),
    workspace('beta-own', 'Running'),
    workspace('gamma-review', 'Stopped', true),
  ]);
  assert.deepEqual(ordered.map(item => item.id), [
    'beta-own',
    'alpha-own',
    'zeta-review',
    'gamma-review',
  ]);
});
