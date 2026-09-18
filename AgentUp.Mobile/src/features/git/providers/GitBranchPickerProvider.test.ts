import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { GitHeadState, GitRemoteBranch } from '../models/GitChanges';
import {
  GIT_BRANCH_PICKER_VISIBLE_ROWS,
  filterGitBranchPickerRows,
  gitBranchMutationConfirm,
  gitBranchPickerViewportHeight,
  gitFetchRemoteLabel,
  gitUpstreamLabel,
} from './GitBranchPickerProvider';

const remotes: GitRemoteBranch[] = [
  { remote: 'origin', name: 'main' },
  { remote: 'origin', name: 'feature-auth' },
  { remote: 'upstream', name: 'release' },
];

function head(overrides: Partial<GitHeadState> = {}): GitHeadState {
  return {
    branch: 'main',
    localBranches: ['main', 'feature-auth', 'docs-ia'],
    remoteBranches: remotes,
    upstream: 'origin/main',
    ...overrides,
  };
}

test('filterGitBranchPickerRows keeps Local and Remote groups and does not truncate past five', () => {
  const local = ['main', 'one', 'two', 'three', 'four', 'five', 'six'];
  const rows = filterGitBranchPickerRows(local, remotes, '');

  assert.equal(GIT_BRANCH_PICKER_VISIBLE_ROWS, 5);
  assert.equal(gitBranchPickerViewportHeight(), 5 * 40);
  assert.equal(rows.filter(row => row.kind === 'local').length, 7);
  assert.equal(rows.filter(row => row.kind === 'remote').length, 3);
  assert.equal(rows[0].kind, 'section');
  assert.equal(rows[0].kind === 'section' && rows[0].title, 'Local');
  assert.ok(rows.some(row => row.kind === 'section' && row.title === 'Remote'));
});

test('filterGitBranchPickerRows searches local and remote names together', () => {
  const rows = filterGitBranchPickerRows(
    ['main', 'feature-auth', 'docs-ia'],
    remotes,
    'AUTH',
  );

  assert.deepEqual(
    rows.filter(row => row.kind !== 'section').map(row => (row.kind === 'local' ? row.name : row.label)),
    ['feature-auth', 'origin/feature-auth'],
  );
});

test('filterGitBranchPickerRows matches a remote prefix and omits empty groups', () => {
  const rows = filterGitBranchPickerRows(['main'], remotes, 'upstream');

  assert.deepEqual(rows, [
    { kind: 'section', title: 'Remote' },
    { kind: 'remote', remote: 'upstream', name: 'release', label: 'upstream/release' },
  ]);
});

test('filterGitBranchPickerRows returns nothing when the query misses every name', () => {
  assert.deepEqual(filterGitBranchPickerRows(['main'], remotes, 'does-not-exist'), []);
});

test('gitBranchMutationConfirm names fetch, pull, push, and force-push remotes', () => {
  const state = head();

  assert.equal(gitFetchRemoteLabel(state), 'all remotes (origin, upstream)');
  assert.equal(gitUpstreamLabel(state), 'origin/main');
  assert.deepEqual(gitBranchMutationConfirm('fetch', state), {
    title: 'Fetch and prune?',
    message: 'This fetches and prunes from all remotes (origin, upstream).',
    confirm: 'Fetch',
  });
  assert.equal(
    gitBranchMutationConfirm('pull', state).message,
    'This fast-forwards the current branch main from origin/main.',
  );
  assert.equal(
    gitBranchMutationConfirm('push', state).message,
    'This pushes the current branch main to origin/main.',
  );

  const force = gitBranchMutationConfirm('forcePush', state);
  assert.equal(force.title, 'Force-push with lease?');
  assert.match(force.message, /force-pushes branch main to origin\/main with --force-with-lease/);
  assert.equal(force.confirm, 'Force push');
  assert.equal(force.destructive, true);
});

test('gitBranchMutationConfirm names switch, remote checkout, and create targets', () => {
  const state = head({ remoteBranches: [{ remote: 'origin', name: 'main' }] });

  assert.equal(gitFetchRemoteLabel(state), 'origin');
  assert.equal(
    gitBranchMutationConfirm('fetch', state).message,
    'This fetches and prunes from remote origin.',
  );
  assert.equal(
    gitBranchMutationConfirm('switch', state, 'docs-ia').message,
    'This checks out the local branch docs-ia in the workspace worktree.',
  );
  assert.equal(
    gitBranchMutationConfirm('checkoutRemote', state, 'origin/feature-auth').message,
    'This checks out origin/feature-auth, creating a local tracking branch if needed.',
  );
  assert.equal(
    gitBranchMutationConfirm('create', state, 'topic').message,
    'This creates branch topic from the current HEAD and checks it out.',
  );
  assert.equal(gitUpstreamLabel(head({ upstream: null })), 'its upstream');
});
