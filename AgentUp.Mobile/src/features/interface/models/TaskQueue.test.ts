import assert from 'node:assert/strict';
import test from 'node:test';
import { issueLabel, offerable, reorderTarget, repositoryLabel, taskLabel, type OfferedIssue, type QueuedTask } from './TaskQueue';

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
