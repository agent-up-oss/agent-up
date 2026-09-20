import assert from 'node:assert/strict';
import test from 'node:test';
import {
  browserServerStorage,
  clearActiveCredential,
  loadServerSelection,
  logoutActiveServer,
  removeServer,
  saveServerSelection,
  selectServer,
  upsertServer,
  type KeyValueStorage,
  type ServerSelection,
} from './ServerStorageProvider';

class MemoryStorage implements KeyValueStorage {
  value: string | null = null;
  getItem() { return this.value; }
  setItem(_key: string, value: string) { this.value = value; }
}

test('server selection is persisted and restored', () => {
  const storage = new MemoryStorage();
  const selection = { servers: [{ id: 'one', url: 'http://localhost:5000' }], activeServerId: 'one' };
  saveServerSelection(storage, selection);
  assert.deepEqual(loadServerSelection(storage), selection);
});

test('invalid persisted state is ignored', () => {
  const storage = new MemoryStorage(); storage.value = '{broken';
  assert.deepEqual(loadServerSelection(storage), { servers: [], activeServerId: null });
});

test('storage write failures do not escape', () => {
  const storage: KeyValueStorage = {
    getItem: () => null,
    setItem: () => { throw new Error('quota exceeded'); },
  };
  assert.doesNotThrow(() => saveServerSelection(storage, { servers: [], activeServerId: null }));
});

test('unavailable browser storage returns null', () => {
  Object.defineProperty(globalThis, 'window', { configurable: true, value: {} });
  Object.defineProperty(globalThis.window, 'localStorage', { configurable: true, get: () => { throw new Error('denied'); } });
  assert.equal(browserServerStorage(), null);
  Reflect.deleteProperty(globalThis, 'window');
});

const empty: ServerSelection = { servers: [], activeServerId: null };

test('upsertServer adds a new server and selects it', () => {
  const next = upsertServer(empty, 'http://localhost:5000', 'token-1');
  assert.equal(next.servers.length, 1);
  assert.equal(next.servers[0].url, 'http://localhost:5000');
  assert.equal(next.servers[0].accessToken, 'token-1');
  assert.equal(next.activeServerId, next.servers[0].id);
});

test('upsertServer keeps the saved token when reconnecting without a new one', () => {
  const saved = upsertServer(empty, 'http://localhost:5000', 'token-1');
  const next = upsertServer(saved, 'http://localhost:5000');
  assert.equal(next.servers.length, 1);
  assert.equal(next.servers[0].accessToken, 'token-1');
  assert.equal(next.activeServerId, saved.servers[0].id);
});

test('selectServer ignores unknown ids', () => {
  const saved = upsertServer(empty, 'http://localhost:5000');
  assert.deepEqual(selectServer(saved, 'missing'), saved);
});

test('clearActiveCredential drops the saved token but keeps the server', () => {
  const saved = upsertServer(empty, 'http://localhost:5000', 'token-1');
  const next = clearActiveCredential(saved);
  assert.equal(next.servers.length, 1);
  assert.equal(next.servers[0].accessToken, undefined);
  assert.equal(next.activeServerId, saved.activeServerId);
});

test('logoutActiveServer drops the token and the active selection', () => {
  const saved = upsertServer(empty, 'http://localhost:5000', 'token-1');
  const next = logoutActiveServer(saved);
  assert.equal(next.servers.length, 1);
  assert.equal(next.servers[0].accessToken, undefined);
  assert.equal(next.activeServerId, null);
});

test('removeServer drops the entry and repoints the active selection', () => {
  const first = upsertServer(empty, 'http://localhost:5000', 'one');
  const both = upsertServer(first, 'https://agent-up.example.com', 'two');
  const remaining = removeServer(both, both.activeServerId!);
  assert.equal(remaining.servers.length, 1);
  assert.equal(remaining.servers[0].url, 'http://localhost:5000');
  assert.equal(remaining.activeServerId, remaining.servers[0].id);
});
