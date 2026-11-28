import assert from 'node:assert/strict';
import test from 'node:test';
import { compareLimit, diffFromTexts, diffFromUnified, diffNewFile, unifiedFromDiff } from './DiffProvider';

test('a unified diff keeps its hunks, numbers and path', () => {
  const diff = diffFromUnified('Index: /repo/server.ts\n===\n--- /repo/server.ts\n+++ /repo/server.ts\n@@ -2,3 +2,3 @@\n import x\n-listen(3000)\n+listen(3001)\n end\n\\ No newline at end of file');
  assert.equal(diff.path, '/repo/server.ts');
  assert.deepEqual(diff.lines.map(line => line.kind), ['hunk', 'context', 'deleted', 'added', 'context']);
  assert.deepEqual([diff.added, diff.removed], [1, 1]);
  assert.deepEqual([diff.lines[2]?.oldNumber, diff.lines[3]?.newNumber, diff.lines[3]?.text], [3, 3, 'listen(3001)']);
});

test('two texts compare to the changed lines with context and a gap between distant changes', () => {
  const before = Array.from({ length: 20 }, (_, index) => `line ${index + 1}`).join('\n');
  const after = before.replace('line 2\n', 'line two\n').replace('line 18', 'line eighteen');
  const diff = diffFromTexts(before, after, 'a.txt');
  assert.deepEqual([diff.added, diff.removed], [2, 2]);
  assert.equal(diff.lines.filter(line => line.kind === 'hunk').length, 1);
  assert.ok(!diff.lines.some(line => line.text === 'line 10'));
});

test('a new file is every line added and huge texts are a whole replacement', () => {
  const created = diffNewFile('a\nb\n', 'new.md');
  const replaced = diffFromTexts(Array.from({ length: compareLimit + 1 }, (_, index) => `${index}`).join('\n'), 'x');
  assert.deepEqual([created.added, created.removed], [2, 0]);
  assert.deepEqual(created.lines.map(line => line.newNumber), [1, 2]);
  assert.deepEqual([replaced.added, replaced.removed], [1, compareLimit + 1]);
});

test('a diff reads back as unified text, one hunk per run at its own line numbers', () => {
  const diff = diffFromTexts('a\nb\nc\nd\ne\nf\ng\nh\ni\nj\n', 'a\nB\nc\nd\ne\nf\ng\nh\ni\nJ\n', 'x.ts');
  const unified = unifiedFromDiff(diff);
  assert.equal(unified.split('\n').filter(line => line.startsWith('@@')).length, 2);
  assert.match(unified, /^@@ -1,5 \+1,5 @@\n a\n-b\n\+B\n c\n d\n e\n/);
  assert.match(unified, /@@ -7,4 \+7,4 @@\n g\n h\n i\n-j\n\+J\n$/);
});

test('a new file reads back as one hunk of added lines', () => {
  assert.equal(unifiedFromDiff(diffNewFile('one\ntwo\n', 'n.ts')), '@@ -0,0 +1,2 @@\n+one\n+two\n');
});
