import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  diffFileLabel,
  diffLineNumber,
  diffMessage,
  diffSegments,
  fileLines,
  paintedPieces,
  parseUnifiedDiff,
  revealMore,
  wordDiff,
  type DiffFold,
  type DiffSegment,
} from './GitDiffProvider';

const file = Array.from({ length: 40 }, (_, index) => `line ${index + 1}`);
// Line 10 is edited and line 30 is removed, each with three lines of context.
const twoHunks = [
  'diff --git a/orders.ts b/orders.ts',
  'index 1..2 100644',
  '--- a/orders.ts',
  '+++ b/orders.ts',
  '@@ -7,7 +7,7 @@',
  ' line 7', ' line 8', ' line 9',
  '-  return store.orders.filter(order => order.open);',
  '+  return store.orders.filter(order => order.status === \'open\');',
  ' line 11', ' line 12', ' line 13',
  '@@ -27,7 +27,6 @@',
  ' line 27', ' line 28', ' line 29',
  '-line 30 gone',
  ' line 30', ' line 31', ' line 32',
  '',
].join('\n');

function folds(segments: DiffSegment[]): DiffFold[] {
  return segments.flatMap(segment => (segment.kind === 'fold' ? [segment.fold] : []));
}

test('a diff becomes numbered lines with no headers', () => {
  const parsed = parseUnifiedDiff(twoHunks);
  const lines = parsed.hunks.flatMap(hunk => hunk.lines);
  assert.equal(parsed.hunks.length, 2);
  assert.deepEqual([parsed.added, parsed.deleted], [1, 2]);
  assert.ok(lines.every(line => !line.text.startsWith('@@') && !line.text.startsWith('--- ')));
  assert.deepEqual(lines.slice(3, 5).map(diffLineNumber), [10, 10]);
});

test('an edited line marks only the words that changed', () => {
  const lines = parseUnifiedDiff(twoHunks).hunks[0].lines;
  const removed = lines[3].words!.filter(span => span.changed).map(span => span.text);
  const added = lines[4].words!.filter(span => span.changed).map(span => span.text);
  // `open` survives inside the new condition, so only what was inserted around it is marked.
  assert.deepEqual(removed, []);
  assert.deepEqual(added, ["status === '", "'"]);
});

test('a changed word is marked on both sides', () => {
  const words = wordDiff('const limit = 10;', 'const limit = 25;')!;
  assert.deepEqual(words.before.filter(span => span.changed).map(span => span.text), ['10']);
  assert.deepEqual(words.after.filter(span => span.changed).map(span => span.text), ['25']);
});

test('a line replaced by something unrelated stays whole', () => {
  assert.equal(wordDiff('const a = 1;', 'export default function Banner() {}'), null);
  assert.equal(wordDiff('same', 'same'), null);
});

test('unchanged stretches fold, counted from the file', () => {
  const segments = diffSegments(parseUnifiedDiff(twoHunks), file);
  assert.deepEqual(folds(segments).map(fold => [fold.from, fold.to, fold.hidden]), [[1, 6, 6], [14, 26, 13], [33, 40, 8]]);
  assert.deepEqual(folds(segments).map(fold => [fold.opensDown, fold.opensUp]), [[false, true], [true, true], [true, false]]);
});

test('without the file a fold still counts what it hides, but cannot open, and the tail is left out', () => {
  const segments = diffSegments(parseUnifiedDiff(twoHunks), null);
  assert.deepEqual(folds(segments).map(fold => [fold.hidden, fold.opensDown, fold.opensUp]), [[6, false, false], [13, false, false]]);
});

test('a fold opens five lines at a time from either side, numbered in both files', () => {
  const parsed = parseUnifiedDiff(twoHunks);
  const middle = folds(diffSegments(parsed, file))[1];
  const reveal = revealMore(revealMore({}, middle, 'down'), middle, 'up');
  const after = diffSegments(parsed, file, reveal);
  const opened = folds(after)[1];
  const shown = after.flatMap(segment => (segment.kind === 'lines' ? segment.lines : []));
  assert.deepEqual([opened.from, opened.to, opened.hidden], [19, 21, 3]);
  assert.ok(shown.some(line => line.newNumber === 18 && line.oldNumber === 18 && line.text === 'line 18'));
  assert.ok(shown.some(line => line.newNumber === 22 && line.text === 'line 22'));
});

test('opening a fold all the way joins the runs on either side', () => {
  const parsed = parseUnifiedDiff(twoHunks);
  const middle = folds(diffSegments(parsed, file))[1];
  const after = diffSegments(parsed, file, revealMore({}, middle, 'all'));
  assert.equal(folds(after).length, 2);
  assert.equal(after.filter(segment => segment.kind === 'lines').length, 1);
});

test('after a removed line, lines below are numbered from the old file too', () => {
  const parsed = parseUnifiedDiff(twoHunks);
  const tail = folds(diffSegments(parsed, file))[2];
  const opened = diffSegments(parsed, file, revealMore({}, tail, 'down', 1));
  const line = opened.flatMap(segment => (segment.kind === 'lines' ? segment.lines : [])).find(item => item.newNumber === 33);
  assert.deepEqual([line?.oldNumber, line?.text], [34, 'line 33']);
});

test('syntax colours and changed words share the same text', () => {
  const pieces = paintedPieces(
    [{ kind: 'keyword', text: 'return ' }, { kind: 'plain', text: 'order.open' }],
    [{ text: 'return order.', changed: false }, { text: 'open', changed: true }],
  );
  assert.deepEqual(pieces, [
    { text: 'return ', kind: 'keyword', changed: false },
    { text: 'order.', kind: 'plain', changed: false },
    { text: 'open', kind: 'plain', changed: true },
  ]);
});

test('an added or a removed file shows no fold at all', () => {
  const added = parseUnifiedDiff('--- /dev/null\n+++ b/a.ts\n@@ -0,0 +1,2 @@\n+one\n+two\n');
  const removed = parseUnifiedDiff('--- a/a.ts\n+++ /dev/null\n@@ -1,2 +0,0 @@\n-one\n-two\n');
  assert.equal(folds(diffSegments(added, ['one', 'two'])).length, 0);
  assert.equal(folds(diffSegments(removed, null)).length, 0);
});

test('the file is named like an editor tab', () => {
  assert.deepEqual(diffFileLabel('apps/api/orders.ts'), { kind: 'TS', name: 'orders.ts', folder: 'apps/api' });
  assert.deepEqual(diffFileLabel('Makefile'), { kind: 'TXT', name: 'Makefile', folder: '' });
});

test('the view says why it has no lines', () => {
  assert.equal(diffMessage(null, 'a'), 'This file no longer has changes.');
  assert.match(diffMessage({ isBinary: true, diff: '' }, 'a')!, /binary/);
  assert.equal(diffMessage({ isBinary: false, diff: '@@ -1 +1 @@\n-a\n+b\n' }, 'a'), null);
  assert.deepEqual(fileLines('a\r\nb\n'), ['a', 'b']);
});
