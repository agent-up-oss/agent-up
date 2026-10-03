import assert from 'node:assert/strict';
import { test } from 'node:test';
import { workspaceScopeKey } from './WorkspaceScopeKeyProvider';

test('workspaceScopeKey distinguishes the same workspace on two servers', () => {
  assert.notEqual(workspaceScopeKey('server-a', 'main'), workspaceScopeKey('server-b', 'main'));
});

test('workspaceScopeKey is stable for one connection and workspace', () => {
  assert.equal(workspaceScopeKey('server-a', 'main'), 'server-a:main');
});
