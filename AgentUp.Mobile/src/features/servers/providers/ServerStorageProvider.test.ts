import assert from 'node:assert/strict';
import test from 'node:test';
import { browserServerStorage, loadServerSelection, saveServerSelection, type KeyValueStorage } from './ServerStorageProvider';

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
