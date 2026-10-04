import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import catalog from '@agent-up/design-system/catalog' with { type: 'json' };
import screenshots from '@agent-up/design-system/screenshots' with { type: 'json' };

const requiredSurfaces = [
  'foundations', 'primitives', 'chrome', 'workspaces', 'applications', 'browser',
  'console', 'git', 'diagnostics', 'metrics', 'validation', 'auth', 'database',
  'mobile', 'documentation', 'marketing', 'voice', 'brand', 'governance',
  'file-viewer', 'screens',
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
  assert.match(css, /@agent-up\/design-system\/docs\.css/);
  assert.match(css, /@agent-up\/design-system\/screenshot-shell\.css/);
  assert.match(css, /clamp\(2\.25rem, 4vw, 2\.75rem\)/);
  assert.doesNotMatch(css, /\.theme-doc-markdown h1 \{[^}]*--au-font-size-ui-xl/);
});

test('the showcase is intro, then assembled screens, then the catalog', async () => {
  const page = await readFile(new URL('../src/pages/design-system/index.js', import.meta.url), 'utf8');
  const css = await readFile(new URL('../src/pages/design-system/index.module.css', import.meta.url), 'utf8');
  const intro = page.indexOf('One design schema');
  const screens = page.indexOf('id="screens"');
  const catalogMark = page.indexOf('id="catalog"');
  assert.ok(intro > -1 && screens > intro && catalogMark > screens);
  assert.match(page, /screenshots\.screens/);
  assert.match(page, /dangerouslySetInnerHTML=\{\{ __html: scene\.html \}\}/);
  // A scene that declares a sequence has to actually play it here, or the motion the design
  // system emits only exists in the manifest.
  assert.match(page, /playSequence\(root, scene\.sequence\)/);
  assert.match(page, /au-chip/);
  assert.match(page, /aria-orientation="horizontal"/);
  assert.match(page, /One central definition/);
  assert.match(page, /au-screenshot-showcase/);
  assert.match(page, /au-screenshot-stage/);
  assert.match(page, /au-screenshot-pair/);
  assert.match(page, /transform: `scale\(\$\{scale\}\)`/);
  assert.match(page, /width: scene\.width/);
  assert.match(page, /height: scene\.height/);
  assert.doesNotMatch(css, /flex: var\(--au-preview-aspect/);
  assert.doesNotMatch(css, /\.previewMobile/);
  assert.doesNotMatch(css, /overflow-x:\s*hidden/);
  assert.match(page, /au-marketing-hero/);
  assert.match(page, /function IntroPlayground/);
  assert.match(page, /playgroundControls/);
  assert.match(page, /catalogById/);
  assert.match(page, /dangerouslySetInnerHTML=\{\{ __html: component\.html \}\}/);
  assert.match(page, /aria-label="Live catalog components"/);
  for (const id of [
    'badge', 'choice', 'workspace', 'app-tab', 'chat-transcript', 'git-change-list',
  ]) {
    assert.match(page, new RegExp(`'${id}'`));
    assert.ok(
      catalog.surfaces.some(surface => surface.components.some(component => component.id === id)),
      `playground control ${id} is missing from the catalog`,
    );
  }
  assert.doesNotMatch(page, /playgroundTasks|playgroundTables|seedFiles|postgres:\/\//);
  assert.doesNotMatch(page, /\.png/);
  assert.match(css, /flex-wrap: wrap/);
  assert.match(css, /\.screenChipRow[\s\S]*overflow-x:\s*auto/);
  assert.ok(screenshots.screens.length >= 8, 'showcase screens are missing');
  for (const screen of screenshots.screens) {
    assert.ok(screen.title, `${screen.id} is missing a title`);
    assert.ok(screen.intro, `${screen.id} is missing an intro`);
    assert.ok(screen.desktopId || screen.mobileId, `${screen.id} has no assembled scene`);
  }
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
  assert.doesNotMatch(css, /\.catalog \{[^}]*overflow-x:\s*auto/);
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

test('MDX wrappers emit the catalog documentation classes', async () => {
  const source = await readFile(new URL('../src/theme/MDXComponents.js', import.meta.url), 'utf8');
  for (const name of [
    'DocEyebrow', 'DocFocus', 'DocMeta', 'DocWhat', 'DocSpine', 'DocBeat',
    'DocContract', 'DocFork', 'DocFacts', 'DocSurfaces', 'DocSurface',
    'DocSteps', 'DocCallout', 'DocNext',
  ]) {
    assert.match(source, new RegExp(`function ${name}`));
  }
  assert.match(source, /au-doc-kicker/);
  assert.match(source, /au-doc-callout__body/);
  assert.match(source, /au-field-label/);
  assert.doesNotMatch(source, /au-eyebrow/);
  assert.doesNotMatch(source, /au-badge au-doc-surface/);
});
