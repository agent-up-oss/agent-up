import assert from 'node:assert/strict';
import { test } from 'node:test';
import { agentUpTheme } from '@agent-up/design-system/native';
import type { GitChangeTree } from '../models/GitChanges';
import {
  allFilePaths,
  canCommitSelection,
  canDiscardSelection,
  filePathsUnder,
  flattenChangeTree,
  isDirectorySelected,
  retainSelectedPaths,
  selectedFilePaths,
  statusColor,
  statusGlyph,
  toggleNodeSelection,
} from './GitChangeTreeProvider';

function sampleTree(): GitChangeTree {
  return {
    workspaceId: 'ws-1',
    branch: 'main',
    fileCount: 3,
    root: {
      name: '',
      path: '',
      directories: [
        {
          name: 'src',
          path: 'src',
          directories: [
            {
              name: 'app',
              path: 'src/app',
              directories: [],
              files: [
                { name: 'main.cs', path: 'src/app/main.cs', status: 'Modified' },
                { name: 'util.cs', path: 'src/app/util.cs', status: 'Added' },
              ],
            },
          ],
          files: [],
        },
      ],
      files: [{ name: 'README.md', path: 'README.md', status: 'Modified' }],
    },
  };
}

test('flattenChangeTree lists a Changes root then directories before files', () => {
  const nodes = flattenChangeTree(sampleTree());

  assert.deepEqual(nodes.map(node => node.name), ['Changes', 'src', 'app', 'main.cs', 'util.cs', 'README.md']);
  assert.deepEqual(nodes.map(node => node.depth), [0, 1, 2, 3, 3, 1]);
  assert.deepEqual(nodes.map(node => node.isDirectory), [true, true, true, false, false, false]);
});

test('flattenChangeTree returns nothing without a tree', () => {
  assert.deepEqual(flattenChangeTree(null), []);
});

test('filePathsUnder returns every file beneath a directory', () => {
  const nodes = flattenChangeTree(sampleTree());

  assert.deepEqual(filePathsUnder(nodes, nodes[0]), ['src/app/main.cs', 'src/app/util.cs', 'README.md']);
  assert.deepEqual(filePathsUnder(nodes, nodes[1]), ['src/app/main.cs', 'src/app/util.cs']);
  assert.deepEqual(filePathsUnder(nodes, nodes[5]), ['README.md']);
});

test('toggling a directory selects every file beneath it', () => {
  const nodes = flattenChangeTree(sampleTree());

  const selected = toggleNodeSelection(nodes, nodes[1], []);

  assert.deepEqual(selected.sort(), ['src/app/main.cs', 'src/app/util.cs']);
  assert.equal(isDirectorySelected(nodes, nodes[1], selected), true);
  assert.equal(isDirectorySelected(nodes, nodes[2], selected), true);
  assert.equal(isDirectorySelected(nodes, nodes[0], selected), false);
});

test('toggling a selected directory clears every file beneath it', () => {
  const nodes = flattenChangeTree(sampleTree());
  const selected = toggleNodeSelection(nodes, nodes[1], []);

  assert.deepEqual(toggleNodeSelection(nodes, nodes[1], selected), []);
});

test('deselecting one file unchecks its ancestor directories', () => {
  const nodes = flattenChangeTree(sampleTree());
  const selected = toggleNodeSelection(nodes, nodes[1], []);

  const afterFile = toggleNodeSelection(nodes, nodes[3], selected);

  assert.deepEqual(afterFile, ['src/app/util.cs']);
  assert.equal(isDirectorySelected(nodes, nodes[1], afterFile), false);
  assert.equal(isDirectorySelected(nodes, nodes[2], afterFile), false);
});

test('selecting every file in a directory checks the directory', () => {
  const nodes = flattenChangeTree(sampleTree());

  let selected = toggleNodeSelection(nodes, nodes[3], []);
  selected = toggleNodeSelection(nodes, nodes[4], selected);

  assert.equal(isDirectorySelected(nodes, nodes[2], selected), true);
  assert.equal(isDirectorySelected(nodes, nodes[1], selected), true);
});

test('toggling the Changes root selects every file', () => {
  const nodes = flattenChangeTree(sampleTree());

  const selected = toggleNodeSelection(nodes, nodes[0], []);

  assert.deepEqual(selected.sort(), ['README.md', 'src/app/main.cs', 'src/app/util.cs']);
  assert.equal(isDirectorySelected(nodes, nodes[0], selected), true);
});

test('selectedFilePaths keeps only files that still exist in the tree', () => {
  const nodes = flattenChangeTree(sampleTree());

  assert.deepEqual(
    selectedFilePaths(nodes, ['README.md', 'src/app/main.cs', 'gone.cs']),
    ['src/app/main.cs', 'README.md'],
  );
});

test('retainSelectedPaths drops files that left the tree', () => {
  const nodes = flattenChangeTree(sampleTree());
  assert.deepEqual(retainSelectedPaths(nodes, ['README.md', 'gone.cs']), ['README.md']);
  assert.equal(allFilePaths(nodes).length, 3);
});

test('discard is offered only for a live selection', () => {
  assert.equal(canDiscardSelection(2, false), true);
  assert.equal(canDiscardSelection(0, false), false);
  assert.equal(canDiscardSelection(2, true), false);
});

test('an empty directory is never reported as selected', () => {
  const nodes = flattenChangeTree({
    workspaceId: 'ws-1',
    branch: 'main',
    fileCount: 0,
    root: { name: '', path: '', directories: [{ name: 'src', path: 'src', directories: [], files: [] }], files: [] },
  });

  assert.equal(isDirectorySelected(nodes, nodes[0], []), false);
});

test('commit is offered only for a selection with a non-blank message', () => {
  assert.equal(canCommitSelection(1, 'fix(App): correct the probe'), true);
  assert.equal(canCommitSelection(0, 'fix(App): correct the probe'), false);
  assert.equal(canCommitSelection(1, ''), false);
  assert.equal(canCommitSelection(1, '   \n\t'), false);
  assert.equal(canCommitSelection(0, ''), false);
});

test('status glyphs and colors distinguish the change kinds', () => {
  assert.equal(statusGlyph('Added'), '+');
  assert.equal(statusGlyph('Deleted'), '−');
  assert.equal(statusGlyph(null), '▸');
  assert.equal(statusColor('Deleted'), agentUpTheme.colors.statusDanger);
  assert.equal(statusColor(null), agentUpTheme.colors.textMuted);
});
