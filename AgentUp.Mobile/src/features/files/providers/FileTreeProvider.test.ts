import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { WorkspaceDirectoryListing, WorkspaceFileEntry } from '../models/WorkspaceFiles';
import {
  emptyFileTree,
  fileSizeLabel,
  fileStatusClass,
  fileStatusGlyph,
  foldersToReload,
  needsListing,
  visibleFileRows,
  withExpanded,
  withListing,
} from './FileTreeProvider';

function dir(path: string, changedFiles = 0): WorkspaceFileEntry {
  return { name: path.slice(path.lastIndexOf('/') + 1), path, kind: 'Directory', size: null, status: null, changedFiles };
}

function file(path: string, status: WorkspaceFileEntry['status'] = null): WorkspaceFileEntry {
  return { name: path.slice(path.lastIndexOf('/') + 1), path, kind: 'File', size: 10, status, changedFiles: 0 };
}

function listing(path: string, entries: WorkspaceFileEntry[], truncated = false): WorkspaceDirectoryListing {
  return { workspaceId: 'ws', path, entries, truncated };
}

const top = listing('', [dir('apps', 1), file('README.md')]);
const apps = listing('apps', [dir('apps/storefront', 1), file('apps/package.json')]);
const storefront = listing('apps/storefront', [file('apps/storefront/ProductGrid.tsx', 'Modified')]);

function paths(state: typeof emptyFileTree): string[] {
  return visibleFileRows(state).map(row => (row.kind === 'entry' ? row.entry.path : row.key));
}

test('the tree starts folded at the top level', () => {
  const state = withListing(emptyFileTree, top);

  assert.deepEqual(paths(state), ['apps', 'README.md']);
});

test('an unfolded folder shows its rows under it, one level deeper', () => {
  const state = withExpanded(withListing(withListing(emptyFileTree, top), apps), 'apps', true);

  const rows = visibleFileRows(state);
  assert.deepEqual(paths(state), ['apps', 'apps/storefront', 'apps/package.json', 'README.md']);
  assert.equal(rows[1].depth, 1);
});

test('folding a folder hides everything under it but remembers its open children', () => {
  let state = withListing(withListing(withListing(emptyFileTree, top), apps), storefront);
  state = withExpanded(withExpanded(state, 'apps', true), 'apps/storefront', true);

  const folded = withExpanded(state, 'apps', false);
  const unfolded = withExpanded(folded, 'apps', true);

  assert.deepEqual(paths(folded), ['apps', 'README.md']);
  assert.ok(paths(unfolded).includes('apps/storefront/ProductGrid.tsx'));
});

test('a folder is asked for only the first time it is unfolded', () => {
  const state = withListing(withListing(emptyFileTree, top), apps);

  assert.equal(needsListing(state, 'apps'), false);
  assert.equal(needsListing(state, 'apps/storefront'), true);
});

test('reload reads the top level and the folders still open, parents first', () => {
  let state = withListing(withListing(withListing(emptyFileTree, top), apps), storefront);
  state = withExpanded(withExpanded(state, 'apps/storefront', true), 'apps', true);

  assert.deepEqual(foldersToReload(state), ['', 'apps', 'apps/storefront']);
  assert.deepEqual(foldersToReload(withExpanded(state, 'apps', false)), ['']);
});

test('a folder the Server cut short ends with a notice', () => {
  const rows = visibleFileRows(withListing(emptyFileTree, listing('', [file('a.txt')], true)));

  const last = rows[rows.length - 1];
  assert.equal(last.kind, 'notice');
  assert.match(last.kind === 'notice' ? last.text : '', /first 1 entries/);
});

test('sizes read the way a person says them', () => {
  assert.equal(fileSizeLabel(null), '');
  assert.equal(fileSizeLabel(612), '612 B');
  assert.equal(fileSizeLabel(1843), '1.8 KB');
  assert.equal(fileSizeLabel(3 * 1024 * 1024), '3 MB');
});

test('status glyphs and classes match the Git change list', () => {
  assert.equal(fileStatusGlyph('Modified'), 'M');
  assert.equal(fileStatusGlyph('Untracked'), '?');
  assert.equal(fileStatusGlyph(null), '');
  assert.equal(fileStatusClass('Added'), 'gitStatusAdded');
  assert.equal(fileStatusClass(null), null);
});
