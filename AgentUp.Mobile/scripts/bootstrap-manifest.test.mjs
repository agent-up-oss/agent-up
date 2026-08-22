import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { createBootstrapManifest } from './bootstrap-manifest.mjs';

test('bootstrap manifest pins every exported file except updater bootstrap files', () => {
  const directory = mkdtempSync(join(tmpdir(), 'agent-up-bootstrap-'));
  try {
    mkdirSync(join(directory, '_expo'));
    writeFileSync(join(directory, 'index.html'), '');
    writeFileSync(join(directory, '_expo', 'bundle.js'), '');
    writeFileSync(join(directory, 'sw.js'), '');
    writeFileSync(join(directory, 'bootstrap-manifest.json'), 'old');

    assert.deepEqual(createBootstrapManifest(directory, '235', 'abc/123'), {
      cacheName: 'agent-up-release-bootstrap-235-abc-123',
      files: ['/_expo/bundle.js', '/index.html'],
    });
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
