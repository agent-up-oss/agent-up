import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const pins = {
  minimatch: { override: '11.0.8', min: [11, 0, 1] },
  glob: { override: '14.0.6', min: [14, 0, 2] },
  picomatch: { override: '$picomatch', min: [4, 0, 8] },
};

function isAtLeast(version, min) {
  const [major, minor, patch] = version.split('-')[0].split('.').map(Number);
  const [minMajor, minMinor, minPatch] = min;
  return major > minMajor
    || (major === minMajor && minor > minMinor)
    || (major === minMajor && minor === minMinor && patch >= minPatch);
}

function resolvedVersions(lockfile, packageName) {
  const packages = lockfile.packages ?? {};
  const versions = [];
  for (const [key, value] of Object.entries(packages)) {
    const isPackage = key === `node_modules/${packageName}` || key.endsWith(`/node_modules/${packageName}`);
    if (isPackage && value.link && value.resolved) {
      versions.push(packages[value.resolved]?.version);
    } else if (isPackage) {
      versions.push(value.version);
    } else if (value.name === packageName && value.version) {
      versions.push(value.version);
    }
  }
  return [...new Set(versions.filter(Boolean))];
}

test('Mobile lockfile pins patched minimatch and glob over Expo 58 and Sentry 8', async () => {
  const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  const lockfile = JSON.parse(await readFile(new URL('../package-lock.json', import.meta.url), 'utf8'));
  const chat = JSON.parse(await readFile(new URL('../../AgentUp.Chat/package.json', import.meta.url), 'utf8'));
  const chatLockfile = JSON.parse(await readFile(new URL('../../AgentUp.Chat/package-lock.json', import.meta.url), 'utf8'));

  assert.match(packageJson.dependencies.expo, /^~58\./);
  assert.match(packageJson.dependencies['@sentry/react-native'], /^~8\./);
  assert.equal(packageJson.devDependencies?.picomatch, 'file:../vendor/picomatch');
  assert.equal(chat.overrides?.minimatch, pins.minimatch.override);
  assert.equal(chat.devDependencies?.picomatch, 'file:../vendor/picomatch');
  assert.equal(chat.overrides?.picomatch, pins.picomatch.override);

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
  const picomatch = require('picomatch');
  // Sentry bundler plugins call glob.glob(); Expo fingerprint constructs Minimatch.
  assert.equal(typeof glob.glob, 'function');
  assert.equal(typeof minimatch.Minimatch, 'function');
  assert.equal(typeof picomatch, 'function');
  const attack = 'a'.repeat(28) + '/';
  const started = Date.now();
  assert.equal(picomatch('+(*a)')(attack), false);
  assert.ok(Date.now() - started < 1000, 'single-branch repeated extglob still backtracks');

  for (const version of resolvedVersions(chatLockfile, 'picomatch')) {
    assert.ok(
      isAtLeast(version, pins.picomatch.min),
      `Chat picomatch@${version} is still in the advisory range; need >= ${pins.picomatch.min.join('.')}`,
    );
  }
});
