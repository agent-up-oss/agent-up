'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const { planMobileVersion, selectProductTag } = require('./plan-mobile-version.js');

test('selects the newest reachable vX.Y.Z product tag', () => {
  const selected = selectProductTag([
    'v1.9.0',
    'android-v1.10.0',
    'ios-v1.10.0',
    'v1.10.0',
    'v1.10.0-rc.1',
    'v2',
  ]);

  assert.deepEqual(selected, { tag: 'v1.10.0', version: '1.10.0', parts: [1, 10, 0] });
});

test('ignores mobile store tags when they sort newer than the product tag', () => {
  assert.equal(selectProductTag(['android-v9.9.9', 'ios-v9.9.9', 'v0.1.0']).version, '0.1.0');
});

test('fails when this branch has no product release tag', () => {
  assert.throws(
    () => planMobileVersion({ tags: ['android-v1.0.0', 'v1.0.0-beta.1'], runNumber: '12' }),
    /Ship a normal ci.yml release first/,
  );
});

test('uses GITHUB_RUN_NUMBER as the store version code', () => {
  assert.deepEqual(planMobileVersion({ tags: ['v1.2.3'], runNumber: '44' }), {
    tag: 'v1.2.3',
    version: '1.2.3',
    versionCode: '44',
  });
});

test('rejects a missing or non-positive run number', () => {
  assert.throws(() => planMobileVersion({ tags: ['v1.0.0'], runNumber: '' }), /positive integer/);
  assert.throws(() => planMobileVersion({ tags: ['v1.0.0'], runNumber: '0' }), /positive integer/);
});
