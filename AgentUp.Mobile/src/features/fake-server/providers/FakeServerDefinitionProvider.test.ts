import assert from 'node:assert/strict';
import test from 'node:test';
import { loadFakeServerDefinition } from './FakeServerDefinitionProvider';
import { fakeServerId, fakeServerUrl } from '../models/FakeServerIdentity';

test('the bundled definition matches the client catalog identity', () => {
  const definition = loadFakeServerDefinition();
  assert.equal(definition.id, fakeServerId);
  assert.equal(definition.url, fakeServerUrl);
  assert.equal(definition.displayName, 'Demo');
  assert.ok(definition.workspaces.length > 0);
});
