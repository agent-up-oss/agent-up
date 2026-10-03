import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import screenshots from '@agent-up/design-system/screenshots' with { type: 'json' };

const page = await readFile(new URL('../src/pages/index.js', import.meta.url), 'utf8');
const css = await readFile(new URL('../src/pages/index.module.css', import.meta.url), 'utf8');

const featureIds = [
  'workspaces',
  'applications',
  'git',
  'review',
  'history',
  'agents',
  'diagnostics',
  'validation',
  'capabilities',
];

test('the homepage hero keeps the product screenshot at its native ratio', () => {
  assert.match(page, /src="\/screenshot\.png"/);
  assert.match(page, /width=\{1440\}/);
  assert.match(page, /height=\{900\}/);
  assert.doesNotMatch(page, /Every major view/);
  assert.doesNotMatch(css, /\.shots/);
});

test('the homepage features are customer-facing slices with a Desktop and Mobile modal', () => {
  assert.match(page, /id="features"/);
  assert.match(page, /au-feature-card/);
  assert.match(page, /au-feature-modal/);
  assert.match(page, /role="dialog"/);
  assert.match(page, /Escape/);
  for (const id of featureIds) {
    assert.match(page, new RegExp(`'${id}'`));
    const screen = screenshots.screens.find(item => item.id === id);
    assert.ok(screen, `assembled screens are missing ${id}`);
    assert.ok(screen.desktopId || screen.mobileId, `${id} has no screenshot`);
  }
  assert.doesNotMatch(page, /'sign-in'/);
  assert.doesNotMatch(page, /'file-viewer'/);
  assert.match(page, /review: 'Commits'/);
  assert.match(page, /capabilities: 'Configuration'/);
});
