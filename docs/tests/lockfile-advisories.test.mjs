import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const pins = {
  tinypool: { override: '2.2.0', min: [2, 1, 2] },
  'serialize-javascript': { override: '7.1.4', min: [7, 1, 3] },
  qs: { override: '6.17.1', min: [6, 16, 1] },
  uuid: { override: '11.1.1', min: [11, 1, 1] },
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

test('docs lockfile pins patched leaves npm audit cannot reach through Docusaurus 3.x', async () => {
  const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  const lockfile = JSON.parse(await readFile(new URL('../package-lock.json', import.meta.url), 'utf8'));
  assert.equal(packageJson.devDependencies?.picomatch, 'file:../vendor/picomatch');

  for (const [name, pin] of Object.entries(pins)) {
    assert.equal(packageJson.overrides?.[name], pin.override);
    const versions = resolvedVersions(lockfile, name);
    assert.ok(versions.length > 0, `${name} must remain in the docs lockfile`);
    for (const version of versions) {
      assert.ok(
        isAtLeast(version, pin.min),
        `${name}@${version} is still in the advisory range; need >= ${pin.min.join('.')}`,
      );
    }
  }

  const Tinypool = (await import('tinypool')).default;
  assert.equal(typeof Tinypool, 'function');

  // webpack-dev-server still loads sockjs, which does require('uuid').v4.
  // uuid 12 is ESM-only, so the pin stays on the last CJS 11.x.
  const uuid = createRequire(import.meta.url)('uuid');
  assert.equal(typeof uuid.v4, 'function');

  const require = createRequire(import.meta.url);
  const picomatch = require('picomatch');
  const utils = require('picomatch/lib/utils.js');
  assert.equal(typeof picomatch, 'function');
  const attack = 'a'.repeat(28) + '/';
  const started = Date.now();
  assert.equal(picomatch('+(*a)')(attack), false);
  assert.ok(Date.now() - started < 1000, 'single-branch repeated extglob still backtracks');
  assert.equal(utils.removeBackslashes('\\foo'), 'foo');
  assert.equal(utils.removeBackslashes('[]'), '[]');
  assert.equal(utils.removeBackslashes('[ab\\]c]'), '[ab\\]c]');
  const openBrackets = '['.repeat(8000);
  const scanStarted = Date.now();
  assert.equal(utils.removeBackslashes(openBrackets), openBrackets);
  assert.ok(Date.now() - scanStarted < 1000, 'removeBackslashes still backtracks on open brackets');
});
