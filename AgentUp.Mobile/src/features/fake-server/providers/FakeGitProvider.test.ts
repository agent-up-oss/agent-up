import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  addAgentWorkingTreeFile,
  commitGitFiles,
  discardGitFiles,
  fetchGitRemote,
  gitChangesPayload,
  gitHeadPayload,
  loadFakeGitState,
  pullGitRemote,
  pushGitRemote,
  switchGitBranch,
} from './FakeGitProvider';

function harborGit() {
  return loadFakeGitState('harbor-shop', {
    changes: {
      workspaceId: 'harbor-shop',
      branch: 'main',
      commit: 'a1b2c3d',
      ahead: 0,
      behind: 0,
      localBranches: ['main'],
      remoteBranches: [{ remote: 'origin', name: 'main' }],
      upstream: 'origin/main',
      root: {
        name: '',
        path: '',
        directories: [{
          name: 'apps',
          path: 'apps',
          directories: [{
            name: 'storefront',
            path: 'apps/storefront',
            directories: [],
            files: [{ name: 'ProductGrid.tsx', path: 'apps/storefront/ProductGrid.tsx', status: 'modified' }],
          }],
          files: [],
        }],
        files: [],
      },
    },
    diffs: {
      'apps/storefront/ProductGrid.tsx': {
        path: 'apps/storefront/ProductGrid.tsx',
        status: 'Modified',
        isBinary: false,
        diff: '--- a/apps/storefront/ProductGrid.tsx\n+++ b/apps/storefront/ProductGrid.tsx\n',
      },
    },
    log: { commits: [{ id: 'a1b2c3d4e5f6', shortId: 'a1b2c3d', parents: [], subject: 'feat', author: 'Demo', timestamp: '2026-09-22T07:30:00Z', refs: ['HEAD', 'main'] }] },
    queue: { entries: [], unassignedFiles: ['apps/storefront/ProductGrid.tsx'], queueWorktreePath: null, baseCommit: 'a1b2c3d4e5f6', tipCommit: 'a1b2c3d4e5f6', generation: 0 },
  })!;
}

test('normalizes lowercase git status and exposes a head snapshot', () => {
  const git = harborGit();
  assert.equal(git.files[0]?.status, 'Modified');
  assert.equal(gitHeadPayload(git).branch, 'main');
  assert.equal(gitChangesPayload(git).fileCount, 1);
});

test('commit and discard mutate the fake working tree', () => {
  const git = harborGit();
  const committed = commitGitFiles(git, ['apps/storefront/ProductGrid.tsx'], 'fix(storefront): featured grid');
  assert.equal(committed.succeeded, true);
  assert.equal(git.files.length, 0);
  assert.equal(git.ahead, 1);
  assert.equal(git.behind, 1);
  assert.match(git.log[0]?.subject ?? '', /featured grid/);

  addAgentWorkingTreeFile(git);
  assert.equal(discardGitFiles(git, ['apps/storefront/PromoBanner.tsx']).succeeded, true);
  assert.equal(git.files.length, 0);
});

test('fetch pull and push update ahead behind and history', () => {
  const git = harborGit();
  fetchGitRemote(git);
  fetchGitRemote(git);
  assert.equal(git.behind, 1);
  pullGitRemote(git);
  pullGitRemote(git);
  assert.equal(git.behind, 0);
  assert.equal(git.log.filter(entry => entry.author === 'origin').length, 1);
  assert.ok(git.log[0]?.refs.includes('HEAD'));
  fetchGitRemote(git);
  pullGitRemote(git);
  assert.equal(git.behind, 0);
  assert.equal(git.log.filter(entry => entry.author === 'origin').length, 1);
  const committed = commitGitFiles(git, ['apps/storefront/ProductGrid.tsx'], 'fix(storefront): featured grid');
  assert.equal(committed.succeeded, true);
  assert.equal(git.behind, 0);
  git.ahead = 2;
  pushGitRemote(git);
  assert.equal(git.ahead, 0);
});

test('branch switch and agent files add pending changes', () => {
  const git = harborGit();
  assert.equal(switchGitBranch(git, 'topic', true).succeeded, true);
  assert.equal(git.branch, 'topic');
  const added = addAgentWorkingTreeFile(git);
  assert.equal(added.status, 'Added');
  assert.equal(gitChangesPayload(git).fileCount, 2);
  assert.ok(git.unassignedFiles.includes(added.path));
});
