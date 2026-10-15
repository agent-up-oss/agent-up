import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { ForgeHosting } from '../models/ForgeHosting';
import { hostingCopy } from './GitHostingCopyProvider';

function hosting(availability: ForgeHosting['availability'], extras: Partial<ForgeHosting> = {}): ForgeHosting {
  return {
    availability,
    moduleId: 'github',
    proposal: null,
    checks: [],
    threads: [],
    mergeability: null,
    detail: null,
    ...extras,
  };
}

test('none hides the hosting row entirely', () => {
  assert.equal(hostingCopy(hosting('none')).visible, false);
  assert.equal(hostingCopy(null).visible, false);
});

test('needsCredential is distinct from unreachable and noProposal', () => {
  assert.match(hostingCopy(hosting('needsCredential')).title, /Sign in/);
  assert.equal(hostingCopy(hosting('unreachable', { detail: 'offline' })).title, 'offline');
  assert.match(hostingCopy(hosting('noProposal')).title, /No open proposal/);
});

test('ready reports checks, thread count and mergeability', () => {
  const copy = hostingCopy(hosting('ready', {
    proposal: { id: '1', number: '42', title: 'Ready', url: 'https://example.test/42', headRef: 'topic', baseRef: 'main', state: 'open' },
    checks: [{ name: 'ci', status: 'completed', conclusion: 'success', url: 'https://example.test/check' }],
    threads: [{ id: 'PRRT_1', url: 'https://example.test/thread', summary: 'nits', isResolved: false }],
    mergeability: { mergeable: true, state: 'clean' },
  }));

  assert.equal(copy.visible, true);
  assert.equal(copy.title, 'Ready');
  assert.equal(copy.checks, 'Checks passed');
  assert.equal(copy.checksTone, 'healthy');
  assert.equal(copy.threads, '1 review thread');
  assert.equal(copy.mergeability, 'Mergeable');
});

test('failed checks and conflicts use semantic labels', () => {
  const copy = hostingCopy(hosting('ready', {
    proposal: { id: '1', number: '42', title: 'Broken', url: 'https://example.test/42', headRef: 'topic', baseRef: 'main', state: 'open' },
    checks: [{ name: 'ci', status: 'completed', conclusion: 'failure', url: 'https://example.test/check' }],
    threads: [
      { id: '1', url: 'https://example.test/1', summary: 'a', isResolved: false },
      { id: '2', url: 'https://example.test/2', summary: 'b', isResolved: false },
    ],
    mergeability: { mergeable: false, state: 'dirty' },
  }));

  assert.equal(copy.checks, 'Checks failed');
  assert.equal(copy.checksTone, 'danger');
  assert.equal(copy.threads, '2 review threads');
  assert.equal(copy.mergeability, 'Conflicts with the base');
});
