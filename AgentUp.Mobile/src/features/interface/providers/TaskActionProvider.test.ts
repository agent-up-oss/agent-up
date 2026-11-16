import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { OfferedIssue, QueuedTask } from '../models/TaskQueue';
import { runTaskAction, type TaskActions } from './TaskActionProvider';

function task(id: string, heldByWorkspaceId: string | null = null): QueuedTask {
  return { id, owner: 'example', repository: 'repository', number: 1, title: id, url: 'u', targetWorkspaceId: null, heldByWorkspaceId };
}

const issue: OfferedIssue = { id: 'I-57', number: 57, title: 'Cart badge lags behind the cart', url: 'u' };

function recording(): TaskActions & { calls: string[] } {
  const calls: string[] = [];
  return {
    calls,
    queue: [task('I-51')],
    working: [task('I-44', 'bob')],
    offered: [issue],
    pick: picked => calls.push(`pick ${picked.id}`),
    remove: removed => calls.push(`remove ${removed.id}`),
    stop: stopped => calls.push(`stop ${stopped.id}`),
  };
}

test('queue picks the offered issue the row names', () => {
  const tasks = recording();
  assert.equal(runTaskAction('queue', 'I-57', tasks), true);
  assert.deepEqual(tasks.calls, ['pick I-57']);
});

test('dequeue removes the waiting task and stop releases the one being worked', () => {
  const tasks = recording();
  runTaskAction('dequeue', 'I-51', tasks);
  runTaskAction('stop', 'I-44', tasks);
  assert.deepEqual(tasks.calls, ['remove I-51', 'stop I-44']);
});

test('a row that is no longer listed changes nothing', () => {
  const tasks = recording();
  assert.equal(runTaskAction('dequeue', 'I-99', tasks), true);
  assert.deepEqual(tasks.calls, []);
});

test('any other action is left to the host', () => {
  const tasks = recording();
  assert.equal(runTaskAction('nav-team', null, tasks), false);
  assert.deepEqual(tasks.calls, []);
});
