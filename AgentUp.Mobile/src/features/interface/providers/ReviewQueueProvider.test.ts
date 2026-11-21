import test from 'node:test';
import assert from 'node:assert/strict';
import type { QueuedTask } from '../models/TaskQueue';
import { reviewLine, reviewQueue } from './ReviewQueueProvider';

function proposed(id: string, change: Partial<QueuedTask>): QueuedTask {
  return {
    id,
    owner: 'harbor',
    repository: 'storefront-web',
    number: Number(id),
    title: `Task ${id}`,
    url: `https://forge.test/${id}`,
    targetWorkspaceId: null,
    heldByWorkspaceId: 'bob',
    stage: 'inReview',
    proposal: { number: '7', url: 'https://forge.test/pulls/7', branch: `agent-up/issue-${id}`, baseBranch: 'main', revision: 2 },
    reviews: [],
    needsReview: ['devReviewer', 'pmReviewer'],
    ...change,
  };
}

test('a revision nobody of the kind took needs a reviewer; one its reviewer holds is under review', () => {
  const tasks = [proposed('1', {}), proposed('2', { devReviewerWorkspaceId: 'hector' })];

  const dev = reviewQueue(tasks, 'devReviewer');

  assert.deepEqual(dev.map(row => [row.task.id, row.standing, row.reviewerId]), [['1', 'waiting', null], ['2', 'reviewing', 'hector']]);
});

test('a kind that already answered the open revision reads as reviewed, with its verdict', () => {
  const task = proposed('3', {
    pmReviewerWorkspaceId: 'lucy',
    needsReview: ['devReviewer'],
    reviews: [{ workspaceId: 'lucy', verdict: 'approve', revision: 2, body: 'ok', kind: 'pmReviewer' }],
  });

  const [row] = reviewQueue([task], 'pmReviewer');

  assert.equal(row?.standing, 'reviewed');
  assert.equal(reviewLine(row!, 'Lucy'), 'Approved by Lucy · revision 2');
});

// A push invalidates every review: an approval of revision 1 does not answer revision 2.
test('a review of an older revision does not count for the open one', () => {
  const task = proposed('4', {
    devReviewerWorkspaceId: 'hector',
    reviews: [{ workspaceId: 'hector', verdict: 'approve', revision: 1, body: 'ok', kind: 'devReviewer' }],
  });

  assert.equal(reviewQueue([task], 'devReviewer')[0]?.standing, 'reviewing');
});

test('a task with no proposal, or back with its author unreviewed by this kind, is not in the queue', () => {
  const tasks = [
    proposed('5', { stage: 'implementing', proposal: null }),
    proposed('6', { stage: 'changesRequested', needsReview: [] }),
  ];

  assert.deepEqual(reviewQueue(tasks, 'pmReviewer'), []);
});

test('a waiting row says it waits and which revision', () => {
  const [row] = reviewQueue([proposed('7', {})], 'devReviewer');

  assert.equal(reviewLine(row!, ''), 'Waiting for a reviewer · revision 2');
});
