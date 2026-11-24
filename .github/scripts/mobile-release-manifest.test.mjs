import assert from 'node:assert/strict';
import test from 'node:test';

import { manifest, readManifest } from './mobile-release-manifest.mjs';

test('a manifest names the binary, its checksum and the version it was built as', () => {
  assert.deepEqual(manifest('ios', '1.4.0', 1801, 'v1.4.0', 'Agent-Up-1.4.0-1801.ipa'), {
    platform: 'ios',
    version: '1.4.0',
    versionCode: '1801',
    sourceTag: 'v1.4.0',
    file: 'Agent-Up-1.4.0-1801.ipa',
    checksum: 'Agent-Up-1.4.0-1801.ipa.sha256',
  });
});

test('a manifest refuses what a store would reject or a path could escape', () => {
  assert.throws(() => manifest('ios', '1.4', 1, 'v1.4.0', 'a.ipa'), /not X\.Y\.Z/);
  assert.throws(() => manifest('ios', '1.4.0', 0, 'v1.4.0', 'a.ipa'), /positive integer/);
  assert.throws(() => manifest('android', '1.4.0', 1, 'v1.4.0', 'a.ipa'), /\.aab/);
  assert.throws(() => manifest('android', '1.4.0', 1, 'v1.4.0', '../a.aab'), /\.aab/);
  assert.throws(() => manifest('web', '1.4.0', 1, 'v1.4.0', 'a.aab'), /Unknown store platform/);
});

test('a release leg reads only the platform it ships, with paths beside the manifest', () => {
  const text = JSON.stringify(manifest('android', '0.0.0', 42, 'ci', 'Agent-Up-0.0.0-42.aab'));
  const read = readManifest(text, 'android', 'artifacts/mobile');
  assert.equal(read.path, 'artifacts/mobile/Agent-Up-0.0.0-42.aab');
  assert.equal(read.checksumPath, 'artifacts/mobile/Agent-Up-0.0.0-42.aab.sha256');
  assert.throws(() => readManifest(text, 'ios', 'artifacts/mobile'), /built for android, not ios/);
});
