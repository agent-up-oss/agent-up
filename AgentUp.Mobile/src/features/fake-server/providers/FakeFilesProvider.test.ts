import assert from 'node:assert/strict';
import test from 'node:test';
import type { FakeGitFile } from './FakeGitProvider';
import { fakeDirectoryListing, fakeFileContent, fakeFileVersion, writeFakeFile, type FakeFilesInput } from './FakeFilesProvider';

function input(changes: FakeGitFile[] = [], edits = new Map<string, string>()): FakeFilesInput {
  return {
    workspaceId: 'bob',
    repository: {
      'README.md': '# Harbor Shop\n',
      'apps/storefront/ProductGrid.tsx': 'export function ProductGrid() {}\n',
      'apps/api/orders.ts': 'export const openOrders = 12;\n',
      'docs/old.md': 'gone\n',
    },
    edits,
    changes,
  };
}

const modified: FakeGitFile = { path: 'apps/storefront/ProductGrid.tsx', status: 'Modified', diff: '' };

test('the top level lists folders first and counts the changes under them', () => {
  const listing = fakeDirectoryListing(input([modified]), null);

  assert.ok(listing.ok);
  assert.deepEqual(listing.value.entries.map(entry => entry.name), ['apps', 'docs', 'README.md']);
  assert.equal(listing.value.entries[0].changedFiles, 1);
});

test('a folder is listed one level deep with each file status', () => {
  const listing = fakeDirectoryListing(input([modified]), 'apps/storefront');

  assert.ok(listing.ok);
  assert.deepEqual(listing.value.entries.map(entry => [entry.path, entry.status]), [['apps/storefront/ProductGrid.tsx', 'Modified']]);
});

test('a deleted file is not on disk and a file the agent added is', () => {
  const changes: FakeGitFile[] = [
    { path: 'docs/old.md', status: 'Deleted', diff: '' },
    { path: 'apps/storefront/PromoBanner.tsx', status: 'Added', diff: '--- /dev/null\n+++ b/apps/storefront/PromoBanner.tsx\n@@ -0,0 +1,1 @@\n+export function PromoBanner() {}\n' },
  ];

  const top = fakeDirectoryListing(input(changes), '');
  const added = fakeFileContent(input(changes), 'apps/storefront/PromoBanner.tsx');

  assert.ok(top.ok && added.ok);
  assert.equal(top.value.entries.some(entry => entry.name === 'docs'), false);
  assert.equal(added.value.content, 'export function PromoBanner() {}\n');
});

test('paths that leave the workspace or name .git are refused', () => {
  const up = fakeDirectoryListing(input(), '../secret');
  const git = fakeFileContent(input(), '.git/config');

  assert.equal(up.ok ? 200 : up.status, 400);
  assert.equal(git.ok ? 200 : git.status, 400);
});

test('a missing folder or file is not found', () => {
  const folder = fakeDirectoryListing(input(), 'nowhere');
  const file = fakeFileContent(input(), 'nowhere.md');

  assert.equal(folder.ok ? 200 : folder.status, 404);
  assert.equal(file.ok ? 200 : file.status, 404);
});

test('saving an edit from the current version marks the file modified in Git', () => {
  const edits = new Map<string, string>();
  const files = input([], edits);
  const opened = fakeFileContent(files, 'README.md');
  assert.ok(opened.ok);

  const saved = writeFakeFile(files, { path: 'README.md', content: '# Harbor Shop\n\nEdited.\n', version: opened.value.version });

  assert.ok(saved.ok);
  assert.equal(saved.value.content.status, 'Modified');
  assert.equal(edits.get('README.md'), '# Harbor Shop\n\nEdited.\n');
  assert.match(saved.value.change!.diff, /\+Edited\./);
});

test('a save from an older version is a conflict and changes nothing', () => {
  const edits = new Map<string, string>();

  const saved = writeFakeFile(input([], edits), { path: 'README.md', content: 'mine', version: fakeFileVersion('older') });

  assert.equal(saved.ok ? 200 : saved.status, 409);
  assert.equal(edits.size, 0);
});

test('a save without content is invalid', () => {
  const saved = writeFakeFile(input(), { path: 'README.md', version: 'v' });

  assert.equal(saved.ok ? 200 : saved.status, 400);
});
