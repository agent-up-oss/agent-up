import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const patchedMajor = 2;
const patchedMinor = 1;
const patchedPatch = 2;
const overrideVersion = '2.2.0';

function isPatched(version) {
  const [major, minor, patch] = version.split('.').map(Number);
  return major > patchedMajor
    || (major === patchedMajor && minor > patchedMinor)
    || (major === patchedMajor && minor === patchedMinor && patch >= patchedPatch);
}

function parentRangeAllowsPatched(range) {
  return !/^[~^]?1(\.|$)/.test(range) && !/^[~^]?0(\.|$)/.test(range);
}

test('docs lockfile does not resolve the tinypool prototype-pollution CVEs', async () => {
  const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  const lockfile = JSON.parse(await readFile(new URL('../package-lock.json', import.meta.url), 'utf8'));
  const docusaurus = lockfile.packages['node_modules/@docusaurus/core'];
  const tinypool = lockfile.packages['node_modules/tinypool'];

  assert.ok(docusaurus, '@docusaurus/core must remain in the docs lockfile');
  assert.ok(tinypool, 'tinypool must remain a resolved docs dependency');

  const requested = docusaurus.dependencies?.tinypool;
  const resolved = tinypool.version;
  assert.equal(typeof requested, 'string');
  assert.ok(
    isPatched(resolved),
    `tinypool ${resolved} is GHSA-5gmw-xhrv-c9v3 / GHSA-85c8-ppgw-ccpr; need >= 2.1.2`,
  );

  if (parentRangeAllowsPatched(requested)) {
    assert.equal(
      packageJson.overrides,
      undefined,
      `@docusaurus/core now asks for tinypool ${requested}; drop the 2.2.0 override`,
    );
    return;
  }

  assert.equal(packageJson.overrides?.tinypool, overrideVersion);
  assert.equal(resolved, overrideVersion);

  const Tinypool = (await import('tinypool')).default;
  assert.equal(typeof Tinypool, 'function');
});
