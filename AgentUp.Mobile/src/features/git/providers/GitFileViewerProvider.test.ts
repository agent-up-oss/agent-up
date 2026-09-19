import assert from 'node:assert/strict';
import test from 'node:test';
import {
  FILE_VIEWER_LINE_HEIGHT,
  fileViewerHunks,
  highlightLine,
  jumpIndex,
  lineBoxNames,
  parseFileDiff,
  splitDiffLines,
  windowedLines,
} from './GitFileViewerProvider';

const sample = `diff --git a/src/app/main.cs b/src/app/main.cs
index 111..222 100644
--- a/src/app/main.cs
+++ b/src/app/main.cs
@@ -1,3 +1,4 @@
 using System;
-public class Old
+public sealed class GitFileDiff
 {
`;

test('parseFileDiff virtualizes unified diffs into numbered line records', () => {
  const lines = parseFileDiff(sample);
  assert.equal(lines[0].kind, 'meta');
  const hunks = fileViewerHunks(lines);
  assert.equal(hunks.length, 1);
  assert.equal(hunks[0].lineIndex, 4);
  const added = lines.find(line => line.kind === 'added');
  const deleted = lines.find(line => line.kind === 'deleted');
  assert.equal(added?.prefix, '+');
  assert.equal(added?.newNumber, 2);
  assert.equal(deleted?.prefix, '−');
  assert.equal(deleted?.oldNumber, 2);
  assert.equal(windowedLines(lines, 0, 2).length, 2);
  assert.ok(FILE_VIEWER_LINE_HEIGHT > 0);
});

test('splitDiffLines does not keep a whole file in one string record', () => {
  const many = Array.from({ length: 1200 }, (_, index) => `line ${index}`).join('\n');
  const lines = splitDiffLines(many);
  assert.equal(lines.length, 1200);
  assert.equal(lines[0], 'line 0');
  assert.equal(lines[1199], 'line 1199');
});

test('jumpIndex finds a line number or hunk header', () => {
  const lines = parseFileDiff(sample);
  assert.equal(jumpIndex(lines, '2'), lines.find(line => line.newNumber === 2 || line.oldNumber === 2)?.index);
  assert.equal(jumpIndex(lines, '@@ -1,3 +1,4 @@'), 4);
  assert.equal(jumpIndex(lines, ''), null);
});

test('highlightLine uses the shared design-system grammar for the file path', () => {
  const lines = parseFileDiff(sample);
  const added = lines.find(line => line.kind === 'added')!;
  const tokens = highlightLine('src/app/main.cs', added);
  assert.ok(tokens.some(token => token.kind === 'keyword' && token.text === 'class'));
  assert.deepEqual(lineBoxNames('added', true), ['fileViewerLine', 'fileViewerLineAdded', 'fileViewerLineCurrent']);
});

test('highlightLine keeps leading spaces and tabs in displayed tokens', () => {
  const spaced = parseFileDiff('@@ -1 +1 @@\n+    return foo;\n')
    .find(line => line.kind === 'added')!;
  assert.equal(spaced.text, '    return foo;');
  const tokens = highlightLine('src/app/main.ts', spaced);
  assert.equal(tokens.map(token => token.text).join(''), '    return foo;');
  assert.equal(tokens[0].text, '    ');

  const tabbed = parseFileDiff('@@ -1 +1 @@\n+\treturn foo;\n')
    .find(line => line.kind === 'added')!;
  assert.equal(tabbed.text, '\treturn foo;');
  const tabTokens = highlightLine('src/app/main.ts', tabbed);
  assert.equal(tabTokens.map(token => token.text).join(''), '\treturn foo;');
  assert.equal(tabTokens[0].text, '\t');
});
