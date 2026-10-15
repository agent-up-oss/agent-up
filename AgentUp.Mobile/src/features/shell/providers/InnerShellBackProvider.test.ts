import assert from 'node:assert/strict';
import { test } from 'node:test';
import { resolveInnerBack } from './InnerShellBackProvider';

test('inner pages go back when the router still has a stack', () => {
  assert.equal(resolveInnerBack(true), 'back');
});

test('inner pages replace the fallback href when the stack is empty', () => {
  assert.equal(resolveInnerBack(false), 'fallback');
});
