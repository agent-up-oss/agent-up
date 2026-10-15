import assert from 'node:assert/strict';
import { test } from 'node:test';
import { cloneConfirmLabel, cloneRevisionLabel, isDerivedWorkspace, workspaceSubtitle } from './WorkspaceLabelProvider';

test('own workspaces use the branch as the subtitle', () => {
  assert.equal(workspaceSubtitle({ branch: 'main' }), 'main');
  assert.equal(isDerivedWorkspace({}), false);
});

test('derived workspaces prefer the requested revision over a detached HEAD', () => {
  assert.equal(workspaceSubtitle({
    derived: true,
    branch: 'HEAD',
    sourceRevision: 'topic',
  }), 'topic');
  assert.equal(isDerivedWorkspace({ derived: true }), true);
});

test('clone dialog labels switch for a review workspace', () => {
  assert.equal(cloneRevisionLabel(false), 'Branch');
  assert.equal(cloneRevisionLabel(true), 'Revision');
  assert.equal(cloneConfirmLabel(false, false), 'Clone');
  assert.equal(cloneConfirmLabel(true, false), 'Start review');
  assert.equal(cloneConfirmLabel(true, true), 'Cloning…');
});
