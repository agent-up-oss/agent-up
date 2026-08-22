import assert from 'node:assert/strict';
import test from 'node:test';
import { zipSync } from 'fflate';
import type { ChannelRelease, InstalledRelease } from '../models/ChannelRelease';
import { getInstalledRelease, isUpgrade, parseReleaseZip } from './WebReleaseInstaller';

const release = (publishedAt: string): ChannelRelease => ({
  channel: '235', sha: 'abcdef0', publishedAt, assetUrl: '', archiveSha256: '0'.repeat(64), requiredFiles: ['index.html'],
});

test('upgrade comparison parses timestamps rather than comparing their text', () => {
  const installed: InstalledRelease = { channel: '235', sha: '1234567', publishedAt: '2026-08-19T05:00:00+02:00' };
  assert.equal(isUpgrade(installed, release('2026-08-19T03:30:00Z')), true);
  assert.equal(isUpgrade(installed, release('2026-08-19T02:30:00Z')), false);
});

test('invalid stored release state is cleared without throwing', () => {
  const values = new Map([['agent-up-active-release', '{invalid']]);
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: {
    getItem: (key: string) => values.get(key) ?? null,
    removeItem: (key: string) => values.delete(key),
  }});
  assert.equal(getInstalledRelease().channel, 'development');
  assert.equal(values.has('agent-up-active-release'), false);
  Reflect.deleteProperty(globalThis, 'localStorage');
});

test('release ZIP accepts required files and rejects traversal paths', () => {
  const valid = zipSync({ 'index.html': new TextEncoder().encode('ok'), '_expo/': new Uint8Array(), '_expo/.routes.json': new Uint8Array() });
  assert.equal(parseReleaseZip(valid, ['index.html', '_expo/.routes.json']).length, 2);
  const traversal = zipSync({ '../index.html': new Uint8Array() });
  assert.throws(() => parseReleaseZip(traversal, ['index.html']), /invalid path/);
});

test('release ZIP rejects expanded payloads over the limit', () => {
  const oversized = zipSync({ 'index.html': new Uint8Array(50 * 1024 * 1024 + 1) });
  assert.throws(() => parseReleaseZip(oversized, ['index.html']), /Expanded release exceeds/);
});
