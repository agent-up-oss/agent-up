import assert from 'node:assert/strict';
import test from 'node:test';
import { loadServerSelection, saveServerSelection, type KeyValueStorage } from './ServerStorageProvider';

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
