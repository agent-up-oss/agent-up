import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('SDK 58 expo-router no longer needs a vendored decode-uri-component override', async () => {
  const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  const lockfile = JSON.parse(await readFile(new URL('../package-lock.json', import.meta.url), 'utf8'));
  const lockPackages = Object.keys(lockfile.packages ?? {});

  assert.match(packageJson.dependencies.expo, /^~58\./);
  assert.match(packageJson.dependencies['expo-router'], /^~58\./);
  assert.equal(packageJson.dependencies['decode-uri-component'], undefined);
  assert.equal(packageJson.overrides?.['decode-uri-component'], undefined);
  assert.equal(
    lockPackages.some((name) => name.includes('decode-uri-component')),
    false,
    'expo-router 57 pulled query-string@7 / decode-uri-component@0.2.2 (GHSA-vcc3-ghjq-m6fr). SDK 58 must not reintroduce it.',
  );
});
