import assert from 'node:assert/strict';
import { test } from 'node:test';
import { stringifyEvalResult } from './RemoteBrowserHandle';

test('stringifyEvalResult keeps strings and stringifies objects', () => {
  assert.equal(stringifyEvalResult('Harbor Shop'), 'Harbor Shop');
  assert.equal(stringifyEvalResult(true), 'true');
  assert.equal(stringifyEvalResult({ ok: true }), '{"ok":true}');
  assert.equal(stringifyEvalResult(null), 'null');
});

test('stringifyEvalResult falls back when JSON cannot serialise a cycle', () => {
  const cycle: { self?: unknown } = {};
  cycle.self = cycle;
  assert.equal(stringifyEvalResult(cycle).includes('object'), true);
});
