import assert from 'node:assert/strict';
import test from 'node:test';
import {
  INTERFACE_STORAGE_KEY,
  browserInterfaceStorage,
  readInterfaceChoice,
  writeInterfaceChoice,
  type KeyValueStorage,
} from './InterfacePreferenceProvider';

function memoryStorage(initial: Record<string, string> = {}): KeyValueStorage & { values: Record<string, string> } {
  const values = { ...initial };
  return {
    values,
    getItem: (key: string) => (key in values ? values[key] : null),
    setItem: (key: string, value: string) => { values[key] = value; },
  };
}

test('a client with nothing stored opens on the classic interface', () => {
  assert.equal(readInterfaceChoice(memoryStorage()), 'classic');
});

test('the command center is only chosen when it was explicitly stored', () => {
  assert.equal(readInterfaceChoice(memoryStorage({ [INTERFACE_STORAGE_KEY]: 'command-center' })), 'command-center');
  assert.equal(readInterfaceChoice(memoryStorage({ [INTERFACE_STORAGE_KEY]: 'anything-else' })), 'classic');
});

test('the choice round-trips through storage', () => {
  const storage = memoryStorage();
  assert.equal(writeInterfaceChoice(storage, 'command-center'), true);
  assert.equal(readInterfaceChoice(storage), 'command-center');
  assert.equal(writeInterfaceChoice(storage, 'classic'), true);
  assert.equal(readInterfaceChoice(storage), 'classic');
});

test('a client with no storage reads classic and reports that it could not store a choice', () => {
  assert.equal(readInterfaceChoice(null), 'classic');
  assert.equal(writeInterfaceChoice(null, 'command-center'), false);
});

test('storage that throws is treated as absent rather than crashing the client', () => {
  const hostile: KeyValueStorage = {
    getItem: () => { throw new Error('denied'); },
    setItem: () => { throw new Error('denied'); },
  };
  assert.equal(readInterfaceChoice(hostile), 'classic');
  assert.equal(writeInterfaceChoice(hostile, 'command-center'), false);
});

test('browser storage is absent rather than throwing when the window denies it', () => {
  const original = Object.getOwnPropertyDescriptor(globalThis, 'window');
  Object.defineProperty(globalThis, 'window', { configurable: true, value: {} });
  Object.defineProperty(globalThis.window, 'localStorage', {
    configurable: true,
    get: () => { throw new Error('denied'); },
  });
  try {
    assert.equal(browserInterfaceStorage(), null);
  } finally {
    if (original) Object.defineProperty(globalThis, 'window', original);
    else delete (globalThis as { window?: unknown }).window;
  }
});
