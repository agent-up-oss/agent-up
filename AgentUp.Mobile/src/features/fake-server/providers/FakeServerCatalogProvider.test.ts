import assert from 'node:assert/strict';
import test from 'node:test';
import { fakeServerEntry, isFakeSelection, upsertFakeServer } from './FakeServerCatalogProvider';
import { fakeServerId, fakeServerUrl } from '../models/FakeServerIdentity';

const empty = { servers: [], activeServerId: null };

test('fakeServerEntry is always the built-in Demo catalog item', () => {
  const entry = fakeServerEntry(empty);
  assert.equal(entry.id, fakeServerId);
  assert.equal(entry.url, fakeServerUrl);
  assert.equal(entry.displayName, 'Demo');
  assert.equal(entry.canRemove, false);
  assert.equal(entry.isFake, true);
  assert.equal(entry.openAccess, undefined);
});

test('upsertFakeServer marks Demo as the open-access active server', () => {
  const next = upsertFakeServer(empty);
  assert.equal(next.activeServerId, fakeServerId);
  assert.equal(next.servers[0].openAccess, true);
  assert.equal(isFakeSelection(next), true);
});
