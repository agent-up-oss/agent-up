import assert from 'node:assert/strict';
import { existsSync, mkdirSync, writeFileSync } from 'node:fs';
import { mkdtempSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import test from 'node:test';
import { cleanDist } from './export-web.mjs';

test('cleanDist removes an existing export directory', () => {
  const root = mkdtempSync(join(tmpdir(), 'agent-up-export-'));
  const dist = join(root, 'dist');
  mkdirSync(join(dist, 'workspaces'), { recursive: true });
  writeFileSync(join(dist, 'workspaces', 'index.html'), '<html>old route</html>');

  cleanDist(dist);

  assert.equal(existsSync(dist), false);
});

test('cleanDist is a no-op when the export directory is missing', () => {
  const root = mkdtempSync(join(tmpdir(), 'agent-up-export-'));
  const dist = join(root, 'missing-dist');

  cleanDist(dist);

  assert.equal(existsSync(dist), false);
});
