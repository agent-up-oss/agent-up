import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  firstUnjudged,
  inCategory,
  nextCard,
  pendingCount,
  recordedBy,
  repositoryKey,
  swipeTaken,
  type ProductDecision,
} from './ProductDecision';

function decision(overrides: Partial<ProductDecision> = {}): ProductDecision {
  return {
    id: 'd1',
    owner: 'acme',
    repository: 'widgets',
    workspaceId: 'ws',
    agent: 'Lucy',
    category: 'product-vision',
    headline: 'Queue the work on the Server rather than the host',
    detail: 'detail',
    evidence: 'evidence',
    evidenceImages: [],
    recordedAt: '2026-01-01T00:00:00Z',
    verdict: 'pending',
    note: null,
    reviewedAt: null,
    ...overrides,
  };
}

test('a swipe up moves to the next card and a swipe down to the one before', () => {
  assert.equal(nextCard(0, 1, 3), 1);
  assert.equal(nextCard(2, -1, 3), 1);
});

test('a deck does not wrap, so running off the end says there is no more that way', () => {
  assert.equal(nextCard(2, 1, 3), 2);
  assert.equal(nextCard(0, -1, 3), 0);
});

test('an empty deck has nowhere to go', () => {
  assert.equal(nextCard(0, 1, 0), 0);
});

test('a short drag is reading the card, not moving off it', () => {
  assert.equal(swipeTaken(-20, 80), 0);
  assert.equal(swipeTaken(20, 80), 0);
});

test('a deliberate throw moves the deck the way it was thrown', () => {
  assert.equal(swipeTaken(-120, 80), 1);
  assert.equal(swipeTaken(120, 80), -1);
});

test('a deck opens on the first decision nobody has judged', () => {
  const decisions = [
    decision({ id: 'a', verdict: 'good' }),
    decision({ id: 'b', verdict: 'bad' }),
    decision({ id: 'c' }),
  ];

  assert.equal(firstUnjudged(decisions), 2);
});

test('a deck with nothing waiting opens at the start', () => {
  assert.equal(firstUnjudged([decision({ verdict: 'good' })]), 0);
});

test('only one category is on screen at a time', () => {
  const decisions = [decision(), decision({ id: 'd2', category: 'gui-decisions' })];

  assert.deepEqual(inCategory(decisions, 'gui-decisions').map(item => item.id), ['d2']);
});

test('the count a tab shows is what is still waiting', () => {
  assert.equal(pendingCount([decision(), decision({ id: 'd2', verdict: 'good' })]), 1);
});

test('a repository is named the way a host names it', () => {
  assert.equal(repositoryKey({ owner: 'acme', repository: 'widgets' }), 'acme/widgets');
});

test('a card says who decided it and what it was about', () => {
  assert.equal(recordedBy(decision()), 'Lucy · Product vision');
});
