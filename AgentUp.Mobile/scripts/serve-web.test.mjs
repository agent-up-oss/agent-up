import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import test from 'node:test';
import { contentTypeFor, resolveDistFile, startStaticWebServer } from './serve-web.mjs';

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

test('rejects malformed percent escapes', () => {
  const root = createFixture();
  assert.equal(resolveDistFile(root, '/%'), null);
  assert.equal(resolveDistFile(root, '/%zz'), null);
});

test('a malformed encoded path is answered with 404 rather than left open', async () => {
  const root = createFixture();
  const server = await startStaticWebServer({
    root,
    host: '127.0.0.1',
    port: 0,
    waitForPort: async () => {},
    log: () => {},
  });

  try {
    const { port } = server.address();
    const response = await fetch(`http://127.0.0.1:${port}/%`);

    assert.equal(response.status, 404);
    // Reading the body proves the response was closed rather than hanging open.
    assert.equal(await response.text(), 'Not found');
  } finally {
    await new Promise(resolve => server.close(resolve));
  }
});
