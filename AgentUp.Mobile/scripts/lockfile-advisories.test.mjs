import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const pins = {
  minimatch: { override: '11.0.8', min: [11, 0, 1] },
  glob: { override: '14.0.6', min: [14, 0, 2] },
};

function isAtLeast(version, min) {
  const [major, minor, patch] = version.split('.').map(Number);
  const [minMajor, minMinor, minPatch] = min;
  return major > minMajor
    || (major === minMajor && minor > minMinor)
    || (major === minMajor && minor === minMinor && patch >= minPatch);
}

function resolvedVersions(lockfile, packageName) {
  return Object.entries(lockfile.packages ?? {})
    .filter(([key]) => key === `node_modules/${packageName}` || key.endsWith(`/node_modules/${packageName}`))
    .map(([, value]) => value.version);
}

test('Mobile lockfile pins patched minimatch and glob over Expo 58 and Sentry 8', async () => {
  const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  const lockfile = JSON.parse(await readFile(new URL('../package-lock.json', import.meta.url), 'utf8'));
  const chat = JSON.parse(await readFile(new URL('../../AgentUp.Chat/package.json', import.meta.url), 'utf8'));
  const chatLockfile = JSON.parse(await readFile(new URL('../../AgentUp.Chat/package-lock.json', import.meta.url), 'utf8'));

  assert.match(packageJson.dependencies.expo, /^~58\./);
  assert.match(packageJson.dependencies['@sentry/react-native'], /^~8\./);
  assert.equal(chat.overrides?.minimatch, pins.minimatch.override);

  for (const [name, pin] of Object.entries(pins)) {
    assert.equal(packageJson.overrides?.[name], pin.override);
    const versions = resolvedVersions(lockfile, name);
    assert.ok(versions.length > 0, `${name} must remain in the Mobile lockfile`);
    for (const version of versions) {
      assert.ok(
        isAtLeast(version, pin.min),
        `${name}@${version} is still in the advisory range; need >= ${pin.min.join('.')}`,
      );
    }
  }

  for (const version of resolvedVersions(chatLockfile, 'minimatch')) {
    assert.ok(
      isAtLeast(version, pins.minimatch.min),
      `Chat minimatch@${version} is still in the advisory range; need >= ${pins.minimatch.min.join('.')}`,
    );
  }

  const require = createRequire(import.meta.url);
  const glob = require('glob');
  const minimatch = require('minimatch');
  // Sentry bundler plugins call glob.glob(); Expo fingerprint constructs Minimatch.
  assert.equal(typeof glob.glob, 'function');
  assert.equal(typeof minimatch.Minimatch, 'function');
});
