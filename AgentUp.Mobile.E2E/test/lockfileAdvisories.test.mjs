import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { Readable } from 'node:stream';
import { pipeline } from 'node:stream/promises';
import test from 'node:test';

const requireVendor = createRequire(new URL('../../vendor/stream-json/package.json', import.meta.url));

const lockfile = JSON.parse(readFileSync(new URL('../package-lock.json', import.meta.url), 'utf8'));
const packageJson = JSON.parse(readFileSync(new URL('../package.json', import.meta.url), 'utf8'));
const appLockfile = JSON.parse(readFileSync(new URL('../../AgentUp.Mobile.E2E.App/package-lock.json', import.meta.url), 'utf8'));
const appPackageJson = JSON.parse(readFileSync(new URL('../../AgentUp.Mobile.E2E.App/package.json', import.meta.url), 'utf8'));

function resolved(source, packageName) {
  const packages = source.packages ?? {};
  return Object.entries(packages)
    .filter(([key, value]) => {
      const isPackage = key === `node_modules/${packageName}` || key.endsWith(`/node_modules/${packageName}`);
      return isPackage || (value.name === packageName && value.version);
    })
    .map(([, value]) => ({
      version: value.link && value.resolved ? packages[value.resolved]?.version : value.version,
    }))
    .filter(({ version }) => Boolean(version));
}

function parse(version) {
  const core = version.split('-')[0];
  return core.split('.').map(Number);
}

function overlayOrPatched(source, packageName, isVulnerable) {
  const versions = resolved(source, packageName);
  assert.ok(versions.length > 0, `${packageName} must remain in the lockfile`);
  const vulnerable = versions.filter(({ version }) => isVulnerable(version));
  assert.deepEqual(vulnerable, [], `${packageName} still has an advisory-range copy`);
}

test('Jest 30 glob copies are patched without replacing rimraf or test-exclude glob 6/7', () => {
  assert.equal(packageJson.overrides?.['@jest/reporters']?.glob, '14.0.6');
  const globs = resolved(lockfile, 'glob');
  const vulnerable = globs.filter(({ version }) => {
    const [major, minor, patch] = parse(version);
    const prerelease = version.includes('-');
    return major > 8 && (major < 14 || (major === 14 && minor === 0 && (patch < 2 || (patch === 2 && prerelease))));
  });
  assert.deepEqual(vulnerable, [], 'glob 9.0.0-14.0.1 is GHSA glob AbortSignal bypass; Jest must resolve 14.0.2+');
  assert.ok(globs.some(({ version }) => version.startsWith('6.') || version.startsWith('7.') || version.startsWith('8.')));
});

test('Expo fingerprint minimatch is patched without replacing Detox glob 6/8', () => {
  assert.equal(appPackageJson.overrides?.['@expo/fingerprint']?.minimatch, '11.0.8');
  const fingerprint = Object.entries(appLockfile.packages ?? {})
    .filter(([key]) => key.endsWith('node_modules/@expo/fingerprint/node_modules/minimatch'))
    .map(([, value]) => value.version);
  assert.deepEqual(fingerprint, ['11.0.8']);
});

test('picomatch copies are the GHSA-fc5q-39xc-vjpf overlay, not registry 4.0.7', () => {
  assert.equal(packageJson.devDependencies?.picomatch, 'file:../vendor/picomatch');
  assert.equal(packageJson.overrides?.picomatch, '$picomatch');
  assert.equal(appPackageJson.devDependencies?.picomatch, 'file:../vendor/picomatch');
  assert.equal(appPackageJson.overrides?.picomatch, '$picomatch');
  const isVulnerable = version => {
    const [major, minor, patch] = parse(version);
    return major < 4 || (major === 4 && minor === 0 && patch <= 7);
  };
  overlayOrPatched(lockfile, 'picomatch', isVulnerable);
  overlayOrPatched(appLockfile, 'picomatch', isVulnerable);
});

test('stream-json overlay keeps bunyamin StreamArray.withParser and Detox jsonl Parser', () => {
  assert.equal(packageJson.devDependencies?.['stream-json'], 'file:../vendor/stream-json');
  assert.equal(packageJson.overrides?.['stream-json'], '$stream-json');
  assert.equal(appPackageJson.devDependencies?.['stream-json'], 'file:../vendor/stream-json');
  assert.equal(appPackageJson.overrides?.['stream-json'], '$stream-json');
  const isVulnerable = version => {
    const [major, minor] = parse(version);
    return major < 3 || (major === 3 && minor < 6);
  };
  overlayOrPatched(lockfile, 'stream-json', isVulnerable);
  overlayOrPatched(appLockfile, 'stream-json', isVulnerable);

  const StreamArray = requireVendor('./streamers/StreamArray.js');
  const JsonlParser = requireVendor('./jsonl/Parser.js');
  assert.equal(typeof StreamArray.withParser, 'function');
  assert.equal(typeof StreamArray.make, 'function');
  assert.equal(StreamArray.make().constructor, StreamArray);
  assert.equal(typeof JsonlParser.make, 'function');
});

test('stream-json overlay parses bunyamin arrays and Detox JSONL without Assembler pollution', async () => {
  const StreamArray = requireVendor('./streamers/StreamArray.js');
  const JsonlParser = requireVendor('./jsonl/Parser.js');

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
    Readable.from(['{"msg":"li', 'ne"}\n']),
    JsonlParser.make({ checkErrors: true }),
    async function* (source) {
      for await (const item of source) {
        jsonlItems.push(item);
      }
    },
  );
  assert.deepEqual(jsonlItems, [{ key: 0, value: { msg: 'line' } }]);

  await assert.rejects(
    pipeline(Readable.from(['']), StreamArray.withParser(), async function* (source) {
      for await (const item of source) {
        yield item;
      }
    }),
    SyntaxError,
  );
});
