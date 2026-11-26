'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const { planPrereleaseVersion } = require('./plan-version.js');

test('uses the newest reachable vX.Y.Z tag as the prerelease base', () => {
  const planned = planPrereleaseVersion({
    tags: ['v1.9.0', 'v1.10.0', 'v1.10.0-18340', 'android-v1.11.0'],
    runNumber: '18341',
    runAttempt: '1',
  });

  assert.deepEqual(planned, { baseVersion: '1.10.0', version: '1.10.0-18341' });
});

test('falls back to 0.0.0 when this branch has no stable product tag', () => {
  const planned = planPrereleaseVersion({
    tags: ['v1.0.0-rc.1', 'android-v1.0.0'],
    runNumber: '12',
    runAttempt: '1',
  });

  assert.deepEqual(planned, { baseVersion: '0.0.0', version: '0.0.0-12' });
});

test('omits the run attempt when this is the first try', () => {
  const planned = planPrereleaseVersion({
    tags: ['v2.0.0'],
    runNumber: '9',
    runAttempt: '1',
  });

  assert.equal(planned.version, '2.0.0-9');
});

test('appends the run attempt after a dot on a retry', () => {
  const planned = planPrereleaseVersion({
    tags: ['v2.0.0'],
    runNumber: '9',
    runAttempt: '2',
  });

  assert.equal(planned.version, '2.0.0-9.2');
});

test('treats a missing run attempt as the first try', () => {
  const planned = planPrereleaseVersion({
    tags: ['v1.2.3'],
    runNumber: '44',
  });

  assert.equal(planned.version, '1.2.3-44');
});

test('rejects a missing or non-positive run number', () => {
  assert.throws(
    () => planPrereleaseVersion({ tags: ['v1.0.0'], runNumber: '', runAttempt: '1' }),
    /GITHUB_RUN_NUMBER is not a positive integer/,
  );
  assert.throws(
    () => planPrereleaseVersion({ tags: ['v1.0.0'], runNumber: '0', runAttempt: '1' }),
    /GITHUB_RUN_NUMBER is not a positive integer/,
  );
});

test('rejects a non-positive run attempt', () => {
  assert.throws(
    () => planPrereleaseVersion({ tags: ['v1.0.0'], runNumber: '3', runAttempt: '0' }),
    /GITHUB_RUN_ATTEMPT is not a positive integer/,
  );
});
