import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveMobilePort } from './mobile-port.mjs';

test('uses the Agent-Up allocated web port', () => {
  assert.equal(resolveMobilePort('10901'), 10901);
});

test('uses the Expo default when no managed port is present', () => {
  assert.equal(resolveMobilePort(undefined), 8081);
});

test('rejects invalid managed ports', () => {
  assert.throws(() => resolveMobilePort('abc'), /numeric/);
  assert.throws(() => resolveMobilePort('70000'), /between/);
});
