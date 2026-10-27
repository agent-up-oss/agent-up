import assert from 'node:assert/strict';
import { test } from 'node:test';
import { evalInWindow, javascriptLiteral, stringifyEvalResult } from './RemoteBrowserHandle';

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

test('javascriptLiteral escapes line and paragraph separators left by JSON.stringify', () => {
  assert.equal(javascriptLiteral('Harbor Shop'), '"Harbor Shop"');
  assert.equal(javascriptLiteral('\u2028ok\u2029').includes('\u2028'), false);
  assert.equal(javascriptLiteral('\u2028ok\u2029').includes('\\u2028'), true);
  assert.equal(javascriptLiteral('\u2028ok\u2029').includes('\\u2029'), true);
});

test('evalInWindow looks up eval on the window object because Window has no eval type', () => {
  assert.equal(evalInWindow({ eval: (code: string) => `${code}!` }, 'Harbor Shop'), 'Harbor Shop!');
  assert.equal(evalInWindow({}, 'Harbor Shop'), null);
});
