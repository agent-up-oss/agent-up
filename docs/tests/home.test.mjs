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
  'history',
  'agents',
  'diagnostics',
  'validation',
  'capabilities',
  'sign-in',
];

test('the homepage opens with the title and the feature grid', () => {
  assert.match(page, /au-marketing-hero__lockup/);
  assert.match(page, /src="\/img\/logo\.svg"/);
  assert.match(page, /Review <span className="au-marketing-hero__emphasis">all<\/span> your projects/);
  assert.match(page, /@agent-up\/design-system\/catalog/);
  assert.match(page, /au-feature-card__preview/);
  assert.match(page, /workspace-head/);
  assert.match(page, /git-log-mobile/);
  assert.match(page, /0 of 2 file\(s\) selected/);
  assert.match(page, /feat: weekly promo/);
  assert.match(page, /au-feature-card--\$\{feature\.id\}/);
  assert.doesNotMatch(page, /agent-up\.example/);
  assert.doesNotMatch(page, /f4ke0000/);
  assert.doesNotMatch(page, /Saved servers/);
  assert.doesNotMatch(page, /au-overlay-header/);
  assert.doesNotMatch(page, /au-git-change-list/);
  assert.doesNotMatch(page, /au-feature-card__shot/);
  assert.doesNotMatch(page, /desktop-workspaces\.png/);
  assert.doesNotMatch(page, /mobile-workspaces\.png/);
  assert.doesNotMatch(page, /screenshot\.png/);
  assert.doesNotMatch(page, /Source isolation/);
  assert.doesNotMatch(page, /Every major view/);
  assert.doesNotMatch(page, /Local runtime control plane/);
  assert.doesNotMatch(page, /to="\/docs\/start\/downloads"/);
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
  assert.doesNotMatch(page, /'review'/);
  assert.doesNotMatch(page, /'file-viewer'/);
  assert.doesNotMatch(page, />Features</);
  assert.doesNotMatch(page, /One source of truth/);
  assert.match(page, /au-muted/);
  assert.match(page, /'sign-in': 'Multi-server support'/);
  assert.match(page, /createPortal/);
  assert.match(page, /preventScroll:\s*true/);
  assert.doesNotMatch(page, /scrollTo/);
  assert.match(page, /capabilities: 'Capability plugins'/);
});
