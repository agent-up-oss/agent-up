import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const require = createRequire(import.meta.url);
const decode = require('../vendor/decode-uri-component');

test('exports a CommonJS function query-string can require', () => {
  assert.equal(typeof decode, 'function');
  assert.equal(decode('hello%20world'), 'hello world');
});

test('rejects non-string input', () => {
  assert.throws(() => decode(1), /Expected `encodedURI` to be of type `string`/);
});

test('decodes malformed percent runs in linear time', () => {
  const payload = '%ab'.repeat(1400);
  const started = process.hrtime.bigint();
  const decoded = decode(payload);
  const elapsedMs = Number(process.hrtime.bigint() - started) / 1e6;

  assert.equal(typeof decoded, 'string');
  assert.ok(elapsedMs < 1000, `decode took ${elapsedMs}ms`);
});

test('pins decode-uri-component through a CommonJS file override', async () => {
  const packageJson = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
  assert.equal(packageJson.dependencies['expo-router'], '~57.0.14');
  assert.equal(packageJson.dependencies['decode-uri-component'], 'file:vendor/decode-uri-component');
  assert.equal(packageJson.overrides['decode-uri-component'], '$decode-uri-component');
});
