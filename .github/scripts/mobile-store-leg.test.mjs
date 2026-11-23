import assert from 'node:assert/strict';
import test from 'node:test';

import { decideStoreLeg } from './mobile-store-leg.mjs';

const push = { event: 'push', actor: 'someone', repository: 'org/repo', headRepository: '', channel: '', platforms: '', platform: 'ios' };

test('a push or same-repository pull request builds and signs, and does not release', () => {
  assert.deepEqual(decideStoreLeg(push), { sign: true, release: false, reason: '' });
  assert.equal(decideStoreLeg({ ...push, event: 'pull_request', headRepository: 'org/repo' }).sign, true);
});

test('Dependabot and fork pull requests record a declared skip', () => {
  assert.match(decideStoreLeg({ ...push, actor: 'dependabot[bot]' }).reason, /Dependabot/);
  assert.equal(decideStoreLeg({ ...push, actor: 'dependabot[bot]' }).sign, false);
  assert.equal(decideStoreLeg({ ...push, event: 'pull_request', headRepository: 'fork/repo' }).sign, false);
});

test('a mobile release dispatch signs the platforms it ships and releases', () => {
  const dispatch = { ...push, event: 'workflow_dispatch', channel: 'beta', platforms: 'both' };
  assert.deepEqual(decideStoreLeg(dispatch), { sign: true, release: true, reason: '' });
  assert.equal(decideStoreLeg({ ...dispatch, platforms: 'ios' }).sign, true);
  assert.deepEqual(decideStoreLeg({ ...dispatch, platforms: 'android' }), { sign: false, release: true, reason: 'This mobile release ships android only.' });
});

test('a dispatch with no mobile channel is an ordinary run that still signs', () => {
  const dispatch = { ...push, event: 'workflow_dispatch', channel: 'none', platforms: 'android' };
  assert.deepEqual(decideStoreLeg(dispatch), { sign: true, release: false, reason: '' });
});

test('an unknown platform is a workflow error, not a skip', () => {
  assert.throws(() => decideStoreLeg({ ...push, platform: 'web' }), /Unknown store platform/);
});
