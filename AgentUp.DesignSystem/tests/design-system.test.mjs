import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { extname, resolve } from 'node:path';
import test from 'node:test';
import { agentUpTheme } from '../dist/native/index.js';

test('compiled native bindings retain canonical semantic roles', () => {
  assert.equal(agentUpTheme.colors.canvas, '#000000');
  assert.equal(agentUpTheme.colors.accent, '#00b850');
  assert.equal(agentUpTheme.colors.statusDanger, '#d84f4f');
  assert.equal(agentUpTheme.radii.md, 8);
  assert.equal(agentUpTheme.spacing['4'], 16);
});

test('canonical CSS covers foundations, product components, and marketing compositions', async () => {
  const css = await readFile(new URL('../src/agent-up.css', import.meta.url), 'utf8');
  const marketing = await readFile(new URL('../src/marketing.css', import.meta.url), 'utf8');
  for (const selector of ['.au-button', '.au-card', '.au-input', '.au-tabs', '.au-badge', '.au-code']) {
    assert.match(css, new RegExp(selector.replace('.', '\\.') + '\\s*[{,]'));
  }
  for (const selector of ['.au-marketing-hero', '.au-product-frame', '.au-proof', '.au-do-dont']) {
    assert.match(marketing, new RegExp(selector.replace('.', '\\.') + '\\s*[{,]'));
  }
});

test('marketing primitives prohibit the retired ambient neon treatment', async () => {
  const marketing = await readFile(new URL('../src/marketing.css', import.meta.url), 'utf8');
  assert.doesNotMatch(marketing, /text-shadow|drop-shadow|radial-gradient/i);
});

test('brand voice fixes identity and lifecycle language', async () => {
  const voice = JSON.parse(await readFile(new URL('../brand/voice.json', import.meta.url), 'utf8'));
  assert.equal(voice.productName, 'Agent-Up');
  assert.match(voice.category, /runtime control plane/i);
  assert.deepEqual(Object.keys(voice.lifecycle), ['available', 'preview', 'experimental', 'planned']);
});

test('product consumers do not redefine canonical colors', async () => {
  const repository = resolve(new URL('../..', import.meta.url).pathname);
  const roots = [
    resolve(repository, 'AgentUp.Desktop'),
    resolve(repository, 'AgentUp.Mobile/src'),
    resolve(repository, 'docs/src'),
  ];
  const extensions = new Set(['.axaml', '.cs', '.css', '.js', '.ts', '.tsx']);
  for (const root of roots) {
    for (const file of await filesUnder(root)) {
      if (!extensions.has(extname(file))) continue;
      if (/\.test\.[^.]+$/.test(file)) continue;
      const source = await readFile(file, 'utf8');
      assert.doesNotMatch(source, /#[0-9a-f]{3,8}\b|rgba?\s*\(/i, `${file} redefines a design-system color`);
    }
  }
});

test('non-code manifests stay synchronized with the canonical dark canvas', async () => {
  const repository = resolve(new URL('../..', import.meta.url).pathname);
  const manifest = JSON.parse(await readFile(resolve(repository, 'AgentUp.Mobile/public/manifest.json'), 'utf8'));
  const expo = JSON.parse(await readFile(resolve(repository, 'AgentUp.Mobile/app.json'), 'utf8'));
  assert.equal(manifest.background_color, agentUpTheme.colors.canvas);
  assert.equal(manifest.theme_color, agentUpTheme.colors.canvas);
  assert.equal(expo.expo.userInterfaceStyle, 'dark');
});

async function filesUnder(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(entries.filter(entry => !['bin', 'obj', 'node_modules', 'build'].includes(entry.name)).map(entry => entry.isDirectory()
    ? filesUnder(resolve(directory, entry.name))
    : [resolve(directory, entry.name)]));
  return nested.flat();
}
