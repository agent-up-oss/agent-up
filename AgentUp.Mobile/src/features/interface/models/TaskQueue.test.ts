import assert from 'node:assert/strict';
import test from 'node:test';
import {
  issueLabel,
  offerable,
  reorderTarget,
  repositoryLabel,
  restingOffset,
  rowShift,
  taskLabel,
  type OfferedIssue,
  type QueuedTask,
} from './TaskQueue';

function task(id: string, number: number, extra: Partial<QueuedTask> = {}): QueuedTask {
  return {
    id,
    owner: 'example',
    repository: 'repository',
    number,
    title: `Task ${number}`,
    url: `https://example.test/${number}`,
    targetWorkspaceId: null,
    heldByWorkspaceId: null,
    ...extra,
  };
}

test('a task reads as its number and title', () => {
  assert.equal(taskLabel(task('I-44', 44)), '#44 · Task 44');
});

test('an offered issue reads the same way a queued one does', () => {
  const issue: OfferedIssue = { id: 'I-57', number: 57, title: 'Cart badge lags', url: 'u' };
  assert.equal(issueLabel(issue), '#57 · Cart badge lags');
});

test('an issue already in the queue is not offered again', () => {
  const issues: OfferedIssue[] = [
    { id: 'I-44', number: 44, title: 'Already queued', url: 'u' },
    { id: 'I-57', number: 57, title: 'Not queued', url: 'u' },
  ];

  assert.deepEqual(offerable(issues, [task('I-44', 44)]).map(issue => issue.id), ['I-57']);
});

test('everything is offered while the queue is empty', () => {
  const issues: OfferedIssue[] = [{ id: 'I-57', number: 57, title: 'Not queued', url: 'u' }];

  assert.equal(offerable(issues, []).length, 1);
});

test('a task names the repository it belongs to', () => {
  assert.equal(repositoryLabel(task('I-44', 44)), 'example/repository');
});

test('a row dragged less than half a row stays where it is', () => {
  assert.equal(reorderTarget(1, 20, 64, 4), 1);
});

test('a row dragged a whole row down lands one position later', () => {
  assert.equal(reorderTarget(1, 64, 64, 4), 2);
});

test('a row dragged up lands earlier', () => {
  assert.equal(reorderTarget(2, -128, 64, 4), 0);
});

test('dragging past the ends parks the row at the end rather than off the queue', () => {
  assert.equal(reorderTarget(0, -500, 64, 3), 0);
  assert.equal(reorderTarget(0, 500, 64, 3), 2);
});

test('a queue with no rows to measure against leaves the position alone', () => {
  assert.equal(reorderTarget(1, 200, 0, 4), 1);
  assert.equal(reorderTarget(1, 200, 64, 0), 1);
});

test('a row the drag never reaches does not step aside', () => {
  // Dragging row 0 down to row 1 leaves row 3 where it is: only the span between the two moves.
  assert.equal(rowShift(3, 0, 1, 74), 0);
});

test('dragging down pulls the rows it passes up one place', () => {
  assert.equal(rowShift(1, 0, 2, 74), -74);
  assert.equal(rowShift(2, 0, 2, 74), -74);
});

test('dragging up pushes the rows it passes down one place', () => {
  assert.equal(rowShift(1, 3, 1, 74), 74);
  assert.equal(rowShift(2, 3, 1, 74), 74);
});

test('the dragged row is not shifted by its own gap', () => {
  // It follows the finger instead, so letting the gap move it as well would double the distance.
  assert.equal(rowShift(2, 2, 0, 74), 0);
});

test('a drag that lands where it started shifts nothing', () => {
  for (const index of [0, 1, 2, 3]) assert.equal(rowShift(index, 2, 2, 74), 0);
});

test('the dropped row rests in the slot it chose', () => {
  assert.equal(restingOffset(0, 2, 74), 148);
  assert.equal(restingOffset(3, 1, 74), -148);
  assert.equal(restingOffset(1, 1, 74), 0);
});

test('a position is measured by the pitch between rows, not the row height alone', () => {
  // A 64pt row with a 10pt gap under it means one position is 74pt. Measuring 64 would make a
  // two-row drag read as three positions and land the task somewhere nobody picked.
  assert.equal(reorderTarget(0, 148, 74, 4), 2);
  assert.equal(reorderTarget(0, 148, 64, 4), 2);
  assert.equal(reorderTarget(0, 222, 74, 4), 3);
});
