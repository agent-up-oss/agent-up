import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';

const lockfile = JSON.parse(readFileSync(new URL('../package-lock.json', import.meta.url), 'utf8'));
const packageJson = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8'));

function resolved(packageName) {
  return Object.entries(lockfile.packages ?? {})
    .filter(([key]) => key === `node_modules/${packageName}` || key.endsWith(`/node_modules/${packageName}`))
    .map(([key, value]) => ({ key, version: value.version }));
}

function parse(version) {
  return version.split('.').map(Number);
}

test('Jest 30 glob copies are patched without replacing rimraf or test-exclude glob 6/7', () => {
  assert.equal(packageJson.overrides?.['@jest/reporters']?.glob, '14.0.6');
  const globs = resolved('glob');
  const vulnerable = globs.filter(({ version }) => {
    const [major, minor, patch] = parse(version);
    return major > 8 && (major < 14 || (major === 14 && minor === 0 && patch <= 1));
  });
  assert.deepEqual(vulnerable, [], 'glob 9.0.0-14.0.1 is GHSA glob AbortSignal bypass; Jest must resolve 14.0.2+');
  assert.ok(globs.some(({ version }) => version.startsWith('6.') || version.startsWith('7.') || version.startsWith('8.')));
});

test('Expo fingerprint minimatch is patched without replacing Detox glob 6/8', () => {
  const app = JSON.parse(readFileSync(new URL('../../AgentUp.Mobile.E2E.App/package.json', import.meta.url), 'utf8'));
  const appLockfile = JSON.parse(readFileSync(new URL('../../AgentUp.Mobile.E2E.App/package-lock.json', import.meta.url), 'utf8'));
  assert.equal(app.overrides?.['@expo/fingerprint']?.minimatch, '11.0.8');
  const fingerprint = Object.entries(appLockfile.packages ?? {})
    .filter(([key]) => key.endsWith('node_modules/@expo/fingerprint/node_modules/minimatch'))
    .map(([, value]) => value.version);
  assert.deepEqual(fingerprint, ['11.0.8']);
});

test('bunyamin still loads stream-json 1.x StreamArray, which 3.x removed', () => {
  const versions = resolved('stream-json').map(({ version }) => version);
  assert.ok(versions.every((version) => parse(version)[0] === 1), versions.join(', '));
});
