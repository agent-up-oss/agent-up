import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import catalog from '@agent-up/design-system/catalog' with { type: 'json' };

const requiredSurfaces = [
  'foundations', 'primitives', 'chrome', 'workspaces', 'applications', 'browser',
  'console', 'git', 'diagnostics', 'metrics', 'validation', 'auth', 'database',
  'mobile', 'marketing', 'voice', 'brand', 'governance',
];

test('the design-system showcase is linked from primary navigation and the footer', async () => {
  const config = await readFile(new URL('../docusaurus.config.js', import.meta.url), 'utf8');
  const footer = config.slice(config.indexOf('footer:'), config.indexOf('prism:'));
  const navbar = config.slice(config.indexOf('navbar:'), config.indexOf('footer:'));
  assert.match(footer, /Design System.*\/design-system/s);
  assert.match(navbar, /to:\s*'\/design-system'/);
  assert.match(navbar, /label:\s*'Design System'/);
});

test('the docs import canonical product and marketing styles', async () => {
  const css = await readFile(new URL('../src/css/custom.css', import.meta.url), 'utf8');
  assert.match(css, /@agent-up\/design-system\/styles\.css/);
  assert.match(css, /@agent-up\/design-system\/marketing\.css/);
});

test('the showcase is a tabbed catalog of every public design-system surface', async () => {
  const page = await readFile(new URL('../src/pages/design-system/index.js', import.meta.url), 'utf8');
  const css = await readFile(new URL('../src/pages/design-system/index.module.css', import.meta.url), 'utf8');
  assert.match(page, /role="tablist"/);
  assert.match(page, /aria-orientation="vertical"/);
  assert.match(page, /role="tabpanel"/);
  assert.match(page, /catalog\.surfaces/);
  assert.match(page, /item\.intro/);
  assert.match(css, /grid-template-columns: minmax\(16rem, 22rem\) minmax\(0, 1fr\)/);
  assert.doesNotMatch(css, /overflow-x:\s*auto/);
  for (const id of requiredSurfaces) {
    assert.ok(catalog.surfaces.some(surface => surface.id === id), `catalog is missing surface ${id}`);
  }
});

test('each showcase surface renders live catalog component examples', () => {
  for (const surface of catalog.surfaces) {
    assert.ok(surface.components.length > 0, `${surface.id} has no component examples`);
    for (const component of surface.components) {
      assert.match(component.html, /class="[^"]*au-/);
    }
  }
});
