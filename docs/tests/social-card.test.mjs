import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const docsRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

function pngSize(bytes) {
  const width = bytes.readUInt32BE(16);
  const height = bytes.readUInt32BE(20);
  return { width, height };
}

test('the social card is a 1200x630 image with a public site URL', async () => {
  const png = await readFile(path.join(docsRoot, 'static', 'img', 'social-card.png'));
  assert.equal(png.subarray(0, 8).toString('binary'), '\x89PNG\r\n\x1a\n');
  const { width, height } = pngSize(png);
  assert.equal(width, 1200);
  assert.equal(height, 630);
  const config = await readFile(path.join(docsRoot, 'docusaurus.config.js'), 'utf8');
  assert.match(config, /image:\s*'img\/social-card\.png'/);
  assert.match(config, /twitter:card/);
  assert.match(config, /summary_large_image/);
  assert.match(config, /DOCS_SITE_URL/);
  assert.doesNotMatch(config, /https:\/\/agent-up\.local'/);
});
