import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import test from 'node:test';
import { contentTypeFor, resolveDistFile } from './serve-web.mjs';

function createFixture() {
  const root = mkdtempSync(join(tmpdir(), 'agent-up-mobile-serve-'));
  writeFileSync(join(root, 'index.html'), '<html>home</html>');
  mkdirSync(join(root, 'workspaces'));
  writeFileSync(join(root, 'workspaces', 'index.html'), '<html>workspaces</html>');
  writeFileSync(join(root, 'app.js'), 'console.log("mobile");');
  return root;
}

test('resolves the root index', () => {
  const root = createFixture();
  assert.equal(resolveDistFile(root, '/'), join(root, 'index.html'));
});

test('resolves nested static routes', () => {
  const root = createFixture();
  assert.equal(resolveDistFile(root, '/workspaces'), join(root, 'workspaces', 'index.html'));
});

test('resolves direct asset files', () => {
  const root = createFixture();
  assert.equal(resolveDistFile(root, '/app.js'), join(root, 'app.js'));
});

test('rejects paths outside the dist root', () => {
  const root = createFixture();
  assert.equal(resolveDistFile(root, '/../outside.txt'), null);
});

test('maps common content types', () => {
  assert.equal(contentTypeFor('/dist/index.html'), 'text/html; charset=utf-8');
  assert.equal(contentTypeFor('/dist/app.js'), 'text/javascript; charset=utf-8');
  assert.equal(contentTypeFor('/dist/manifest.json'), 'application/json; charset=utf-8');
});
