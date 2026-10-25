import assert from 'node:assert/strict';
import test from 'node:test';
import { holderLabel, issueLabel, offerable, targetLabel, taskLabel, type OfferedIssue, type QueuedTask } from './TaskQueue';

function task(id: string, number: number, extra: Partial<QueuedTask> = {}): QueuedTask {
  return {
    id,
    owner: 'harbor-oss',
    repository: 'harbor',
    number,
    title: `Task ${number}`,
    url: `https://example.test/${number}`,
    targetWorkspaceId: null,
    heldByWorkspaceId: null,
    ...extra,
  };
}

const names = new Map([['bob', 'Bob'], ['hector', 'Hector']]);

test('a task reads as its number and title', () => {
  assert.equal(taskLabel(task('I-44', 44)), '#44 · Task 44');
});

test('an offered issue reads the same way a queued one does', () => {
  const issue: OfferedIssue = { id: 'I-57', number: 57, title: 'Cart badge lags', url: 'u' };
  assert.equal(issueLabel(issue), '#57 · Cart badge lags');
});

test('a task nobody is pointed at is for anyone', () => {
  assert.equal(targetLabel(task('I-44', 44), names), 'anyone');
});

test('a task pointed at a workspace names that agent', () => {
  assert.equal(targetLabel(task('I-51', 51, { targetWorkspaceId: 'hector' }), names), 'Hector');
});

test('a workspace the team does not know falls back to its id', () => {
  assert.equal(targetLabel(task('I-51', 51, { targetWorkspaceId: 'stranger' }), names), 'stranger');
});

test('a free task says so rather than leaving the column blank', () => {
  assert.equal(holderLabel(task('I-48', 48), names), 'free');
});

test('a held task names the agent carrying it', () => {
  assert.equal(holderLabel(task('I-44', 44, { heldByWorkspaceId: 'bob' }), names), 'Bob');
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
