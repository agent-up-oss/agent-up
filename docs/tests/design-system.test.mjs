import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

test('the design-system showcase is reachable from the footer but absent from primary navigation', async () => {
  const config = await readFile(new URL('../docusaurus.config.js', import.meta.url), 'utf8');
  const footer = config.slice(config.indexOf('footer:'), config.indexOf('prism:'));
  const navbar = config.slice(config.indexOf('navbar:'), config.indexOf('footer:'));
  assert.match(footer, /Design System.*\/design-system/s);
  assert.doesNotMatch(navbar, /Design System|\/design-system/);
});

test('the docs import canonical product and marketing styles', async () => {
  const css = await readFile(new URL('../src/css/custom.css', import.meta.url), 'utf8');
  assert.match(css, /@agent-up\/design-system\/styles\.css/);
  assert.match(css, /@agent-up\/design-system\/marketing\.css/);
});

test('the showcase covers every public design-system area', async () => {
  const page = await readFile(new URL('../src/pages/design-system.js', import.meta.url), 'utf8');
  for (const id of ['foundations', 'components', 'product', 'marketing', 'voice', 'brand', 'governance']) {
    assert.match(page, new RegExp(`id="${id}"`));
  }
});
