import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import test from 'node:test';

const requireOverlay = createRequire(import.meta.url);

const lockfile = JSON.parse(readFileSync(new URL('../package-lock.json', import.meta.url), 'utf8'));
const packageJson = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8'));

function resolved(packageName) {
  const packages = lockfile.packages ?? {};
  return Object.entries(packages)
    .filter(([key, value]) => {
      const isPackage = key === `node_modules/${packageName}` || key.endsWith(`/node_modules/${packageName}`);
      return isPackage || (value.name === packageName && value.version);
    })
    .map(([key, value]) => ({
      key,
      version: value.link && value.resolved ? packages[value.resolved]?.version : value.version,
    }))
    .filter(({ version }) => Boolean(version));
}

function parse(version) {
  return version.split('-')[0].split('.').map(Number);
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

test('picomatch copies are the GHSA-fc5q-39xc-vjpf overlay, not registry 4.0.7', () => {
  const app = JSON.parse(readFileSync(new URL('../../AgentUp.Mobile.E2E.App/package.json', import.meta.url), 'utf8'));
  assert.equal(packageJson.devDependencies?.picomatch, 'file:../vendor/picomatch');
  assert.equal(packageJson.overrides?.picomatch, '$picomatch');
  assert.equal(app.devDependencies?.picomatch, 'file:../vendor/picomatch');
  assert.equal(app.overrides?.picomatch, '$picomatch');
  const versions = resolved('picomatch');
  assert.ok(versions.length > 0, 'picomatch must remain in the E2E lockfile');
  const vulnerable = versions.filter(({ version }) => {
    const [major, minor, patch] = parse(version);
    return major < 4 || (major === 4 && minor === 0 && patch <= 7);
  });
  assert.deepEqual(vulnerable, [], 'picomatch <= 4.0.7 is GHSA-fc5q-39xc-vjpf');
});

test('stream-json overlay keeps bunyamin StreamArray.withParser and Detox jsonl Parser', () => {
  const app = JSON.parse(readFileSync(new URL('../../AgentUp.Mobile.E2E.App/package.json', import.meta.url), 'utf8'));
  assert.equal(packageJson.devDependencies?.['stream-json'], 'file:../vendor/stream-json');
  assert.equal(packageJson.overrides?.['stream-json'], '$stream-json');
  assert.equal(app.devDependencies?.['stream-json'], 'file:../vendor/stream-json');
  assert.equal(app.overrides?.['stream-json'], '$stream-json');
  const versions = resolved('stream-json');
  assert.ok(versions.length > 0, 'stream-json must remain in the E2E lockfile');
  const vulnerable = versions.filter(({ version }) => {
    const [major, minor] = parse(version);
    return major < 3 || (major === 3 && minor < 6);
  });
  assert.deepEqual(vulnerable, [], 'stream-json < 3.6.0 still matches the Assembler/JSONC/filter advisories');

  const StreamArray = requireOverlay('stream-json/streamers/StreamArray.js');
  const JsonlParser = requireOverlay('stream-json/jsonl/Parser');
  assert.equal(typeof StreamArray.withParser, 'function');
  assert.equal(typeof JsonlParser.make, 'function');
});

test('stream-json overlay parses bunyamin arrays and Detox JSONL without Assembler pollution', async () => {
  const { Readable } = requireOverlay('node:stream');
  const { pipeline } = requireOverlay('node:stream/promises');
  const StreamArray = requireOverlay('stream-json/streamers/StreamArray.js');
  const JsonlParser = requireOverlay('stream-json/jsonl/Parser');

  const arrayItems = [];
  await pipeline(
    Readable.from(['[{"msg":"hello","__proto__":{"polluted":true}}]']),
    StreamArray.withParser(),
    async function* (source) {
      for await (const item of source) {
        arrayItems.push(item);
      }
    },
  );
  assert.equal(arrayItems.length, 1);
  assert.equal(arrayItems[0].key, 0);
  assert.equal(arrayItems[0].value.msg, 'hello');
  assert.equal(Object.getPrototypeOf(arrayItems[0].value), Object.prototype);
  assert.equal(Object.prototype.polluted, undefined);

  const jsonlItems = [];
  await pipeline(
    Readable.from(['{"msg":"line"}\n']),
    JsonlParser.make({ checkErrors: true }),
    async function* (source) {
      for await (const item of source) {
        jsonlItems.push(item);
      }
    },
  );
  assert.deepEqual(jsonlItems, [{ key: 0, value: { msg: 'line' } }]);
});
