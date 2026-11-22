import assert from 'node:assert/strict';
import { test } from 'node:test';
import { caretLabel, editorStateLabel, fileNameOf, fileNotice, lineCount, lineNumbers, statusLabel } from './FileEditorProvider';

const content = { path: 'README.md', size: 4, version: 'v', status: null, isBinary: false, isTooLarge: false, content: 'x' };

test('lines are counted the way the gutter numbers them', () => {
  assert.equal(lineCount(''), 1);
  assert.equal(lineCount('one\ntwo\n'), 3);
  assert.equal(lineNumbers(3), '1\n2\n3');
});

test('the caret reads as line and column', () => {
  assert.equal(caretLabel('one\ntwo', 0), 'Ln 1, Col 1');
  assert.equal(caretLabel('one\ntwo', 4), 'Ln 2, Col 1');
  assert.equal(caretLabel('one\ntwo', 99), 'Ln 2, Col 4');
});

test('a file with no editor says why', () => {
  assert.equal(fileNotice(content), null);
  assert.match(fileNotice(null) ?? '', /no longer in the workspace/);
  assert.match(fileNotice({ ...content, content: null, isBinary: true }) ?? '', /binary/);
  assert.match(fileNotice({ ...content, content: null, isTooLarge: true }) ?? '', /more than the Server sends/);
});

test('the footer says saved, unsaved, saving, read only, or the error', () => {
  const base = { dirty: false, saving: false, canWrite: true, hasContent: true, error: null };

  assert.equal(editorStateLabel(base), 'Saved');
  assert.equal(editorStateLabel({ ...base, dirty: true }), 'Unsaved changes');
  assert.equal(editorStateLabel({ ...base, dirty: true, saving: true }), 'Saving…');
  assert.match(editorStateLabel({ ...base, canWrite: false }), /^Read only/);
  assert.equal(editorStateLabel({ ...base, error: 'boom' }), 'boom');
});

test('the status and the file name come from the path the Server reports', () => {
  assert.equal(statusLabel(null), 'Unchanged');
  assert.equal(statusLabel({ ...content, status: 'Modified' }), 'Modified');
  assert.equal(fileNameOf('apps/storefront/ProductGrid.tsx'), 'ProductGrid.tsx');
});
