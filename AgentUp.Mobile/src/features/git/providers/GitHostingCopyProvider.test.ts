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
    remedy: null,
    ...extras,
  };
}

test('null hosting stays hidden until a document arrives', () => {
  assert.equal(hostingCopy(null).visible, false);
});

test('notInstalled is visible with the server detail and remedy', () => {
  const copy = hostingCopy(hosting('notInstalled', {
    moduleId: null,
    detail: 'No forge capability package is enabled.',
    remedy: 'Run: agent-up capabilities bootstrap',
  }));

  assert.equal(copy.visible, true);
  assert.match(copy.title, /forge capability/);
  assert.match(copy.remedy, /capabilities bootstrap/);
  assert.equal(copy.canSignIn, false);
});

test('none is still visible rather than an empty area', () => {
  assert.equal(hostingCopy(hosting('none', { moduleId: null })).visible, true);
  assert.match(hostingCopy(hosting('none', { moduleId: null })).title, /forge capability/);
});

test('needsCredential is distinct from unreachable, noRemote and noProposal', () => {
  assert.equal(hostingCopy(hosting('needsCredential', {
    detail: 'This workspace has no repository host credential.',
    remedy: 'Sign in to your repository host.',
  })).canSignIn, true);
  assert.equal(hostingCopy(hosting('unreachable', { detail: 'offline' })).title, 'offline');
  assert.match(hostingCopy(hosting('noRemote', { detail: 'No repository host origin is configured.' })).title, /origin/);
  assert.match(hostingCopy(hosting('noProposal', { detail: 'No open proposal on this branch.' })).title, /No open proposal/);
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

test('fallback titles cover each availability when detail is blank', () => {
  assert.match(hostingCopy(hosting('notInstalled', { moduleId: null, detail: null })).title, /forge capability/);
  assert.match(hostingCopy(hosting('needsCredential', { detail: ' ' })).title, /credential/);
  assert.match(hostingCopy(hosting('noRemote', { detail: null })).title, /origin/);
  assert.equal(hostingCopy(hosting('unreachable', { detail: null })).title, 'Could not reach the host.');
  assert.match(hostingCopy(hosting('noProposal', { detail: '' })).title, /No open proposal/);
});

test('ready without a proposal title uses Open proposal and pending checks', () => {
  const copy = hostingCopy(hosting('ready', {
    proposal: null,
    checks: [{ name: 'ci', status: 'in_progress', conclusion: '', url: 'https://example.test/check' }],
    mergeability: null,
  }));

  assert.equal(copy.title, 'Open proposal');
  assert.equal(copy.checks, 'Checks pending');
  assert.equal(copy.checksTone, 'warning');
  assert.equal(copy.threads, '0 review threads');
  assert.equal(copy.mergeability, 'Mergeability unknown');
});

test('ready with no checks reports No checks', () => {
  const copy = hostingCopy(hosting('ready', {
    proposal: { id: '1', number: '42', title: 'Bare', url: 'https://example.test/42', headRef: 'topic', baseRef: 'main', state: 'open' },
    mergeability: { mergeable: true, state: 'clean' },
  }));

  assert.equal(copy.checks, 'No checks');
  assert.equal(copy.checksTone, 'healthy');
});
