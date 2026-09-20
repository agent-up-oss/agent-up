import assert from 'node:assert/strict';
import { test } from 'node:test';
import { workspaceOverviewTab, workspaceTabHref } from './WorkspaceTabProvider';

test('workspaceOverviewTab treats the workspace home as Apps', () => {
  assert.equal(workspaceOverviewTab('/workspace/abc', 'abc'), 'apps');
  assert.equal(workspaceOverviewTab('/(main)/workspace/abc/', 'abc'), 'apps');
});

test('workspaceOverviewTab recognizes Git, Agents, and Settings overview routes', () => {
  assert.equal(workspaceOverviewTab('/workspace/abc/git', 'abc'), 'git');
  assert.equal(workspaceOverviewTab('/workspace/abc/agents', 'abc'), 'agents');
  assert.equal(workspaceOverviewTab('/workspace/abc/settings', 'abc'), 'settings');
});

test('workspaceOverviewTab hides the bar on inner pages', () => {
  assert.equal(workspaceOverviewTab('/workspace/abc/git/review', 'abc'), null);
  assert.equal(workspaceOverviewTab('/workspace/abc/git/history', 'abc'), null);
  assert.equal(workspaceOverviewTab('/workspace/abc/agent', 'abc'), null);
  assert.equal(workspaceOverviewTab('/workspace/abc/application/web', 'abc'), null);
});

test('workspaceTabHref stays under the workspace id', () => {
  assert.equal(workspaceTabHref('ws 1', 'apps'), '/(main)/workspace/ws 1');
  assert.equal(workspaceTabHref('ws 1', 'git'), '/(main)/workspace/ws 1/git');
  assert.equal(workspaceTabHref('ws 1', 'agents'), '/(main)/workspace/ws 1/agents');
  assert.equal(workspaceTabHref('ws 1', 'settings'), '/(main)/workspace/ws 1/settings');
});
