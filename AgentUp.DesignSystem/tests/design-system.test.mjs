import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { extname, resolve } from 'node:path';
import test from 'node:test';
import { agentUpTheme, auBox, auText } from '../dist/native/index.js';
import catalog from '../dist/web/catalog.json' with { type: 'json' };

test('compiled native bindings retain canonical semantic roles and component styles', () => {
  assert.equal(agentUpTheme.colors.canvas, '#000000');
  assert.equal(agentUpTheme.colors.accent, '#00b850');
  assert.equal(agentUpTheme.colors.statusDanger, '#d84f4f');
  assert.equal(agentUpTheme.radii.md, 8);
  assert.equal(agentUpTheme.spacing['4'], 16);
  assert.equal(agentUpTheme.controls.height, 44);
  assert.equal(agentUpTheme.typography.sizeSm, 14);
  assert.equal(agentUpTheme.components.button.backgroundColor, '#00b850');
  assert.equal(agentUpTheme.components.button.minHeight, 44);
  assert.equal(agentUpTheme.components.workspace.backgroundColor, '#1c1c1c');
  assert.equal(agentUpTheme.components.workspace.borderColor, '#00000000');
  assert.equal(agentUpTheme.components.workspaceSelected.backgroundColor, '#0f7a45');
  assert.equal(auBox('workspace', 'workspaceSelected').backgroundColor, '#0f7a45');
  assert.equal(auText('workspaceName').color, '#f5fbf7');
  assert.equal(auText('pageTitle').fontSize, 22);
  assert.equal(auBox('cardSelected').borderColor, '#2b2b2b');
  assert.equal(auBox('statusDot', 'statusDotHealthy').backgroundColor, '#22c55e');
});

test('canonical CSS covers foundations, product components, and marketing compositions', async () => {
  const css = await readFile(new URL('../src/agent-up.css', import.meta.url), 'utf8');
  const product = await readFile(new URL('../src/product.css', import.meta.url), 'utf8');
  const marketing = await readFile(new URL('../src/marketing.css', import.meta.url), 'utf8');
  for (const selector of ['.au-button', '.au-card', '.au-input', '.au-tabs', '.au-badge', '.au-code', '.au-checkbox']) {
    assert.match(css, new RegExp(selector.replace('.', '\\.') + '\\s*[{,]'));
  }
  for (const selector of ['.au-workspace', '.au-workspace-name', '.au-app-tab', '.au-console', '.au-git-row', '.au-error-banner', '.au-chrome']) {
    assert.match(product, new RegExp(selector.replace('.', '\\.') + '\\s*[{,]'));
  }
  for (const selector of ['.au-marketing-hero', '.au-product-frame', '.au-proof', '.au-do-dont']) {
    assert.match(marketing, new RegExp(selector.replace('.', '\\.') + '\\s*[{,]'));
  }
});

test('Avalonia bindings include inferred structure, not only colors', async () => {
  const theme = await readFile(new URL('../dist/avalonia/AgentUpTheme.axaml', import.meta.url), 'utf8');
  const styles = await readFile(new URL('../dist/avalonia/AgentUpStyles.axaml', import.meta.url), 'utf8');
  assert.match(theme, /x:Key="AgentUpColorAccent"/);
  assert.match(theme, /x:Key="AgentUpControlHeight"/);
  assert.match(theme, /x:Key="AgentUpCornerRadiusMd"/);
  assert.match(theme, /x:Key="AgentUpFontSizeSm"/);
  assert.match(styles, /Selector="Button\.au-button"/);
  assert.match(styles, /Selector="Border\.au-workspace"/);
  assert.match(styles, /Selector="Border\.wsEntry"/);
  assert.match(styles, /Selector="ListBoxItem:selected Border\.wsEntry"/);
  assert.match(styles, /MinHeight/);
  assert.match(styles, /CornerRadius/);
  const button = styles.match(/<Style Selector="Button\.au-button">([\s\S]*?)<\/Style>/)[1];
  assert.match(button, /AgentUpColorAccentBrush/);
  assert.doesNotMatch(button, /AgentUpColorStatusDangerBrush/);
});

test('Desktop does not restate catalog component styles in MainWindow', async () => {
  const axaml = await readFile(new URL('../../AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml', import.meta.url), 'utf8');
  const styles = axaml.slice(axaml.indexOf('<Window.Styles>'), axaml.indexOf('</Window.Styles>'));
  const restated = [
    'Border.wsEntry', 'Border.appTab', 'Border.subTab', 'Border.wsAvatar',
    'Border.wsAddButton', 'Border.wsLifecycleButton', 'Border.browserButton',
    'Border.gitNodeRow', 'Border.metricsSummaryCard', 'Border.metricsChartCard',
    'Border.tutorialButton', 'Border.dbListItem', 'Border.auditTableRow',
    'Border.auditPageJumpInner', 'Button.dbRunButton', 'Button.wsDeleteButton',
  ];
  for (const selector of restated) {
    assert.doesNotMatch(
      styles,
      new RegExp(`Selector="${selector.replaceAll('.', '\\.')}"`),
      `MainWindow Window.Styles restates ${selector}`
    );
  }
  const addressBar = styles.match(/<Style Selector="TextBox\.addressBar">([\s\S]*?)<\/Style>/)[1];
  assert.doesNotMatch(addressBar, /Property="Background"/);
  assert.match(axaml, /Classes="au-sign-in"/);
  assert.match(axaml, /Classes="au-error-banner"/);
  assert.match(axaml, /Classes="wsName"/);
  assert.match(axaml, /Classes="au-rail"/);
});

test('Mobile applies compiled catalog component styles instead of restating chrome', async () => {
  const repository = resolve(new URL('../..', import.meta.url).pathname);
  const roots = [resolve(repository, 'AgentUp.Mobile/src')];
  const extensions = new Set(['.ts', '.tsx']);
  const files = [];
  for (const root of roots) files.push(...await filesUnder(root));
  const sources = [];
  for (const file of files) {
    if (!extensions.has(extname(file))) continue;
    if (/\.test\.[^.]+$/.test(file)) continue;
    sources.push({ file, source: await readFile(file, 'utf8') });
  }
  for (const { file, source } of sources) {
    assert.doesNotMatch(
      source,
      /border(?:Top|Bottom|Left|Right)?Color:\s*agentUpTheme\.colors\.borderSelected/,
      `${file} uses the selected border as default chrome`,
    );
    assert.doesNotMatch(
      source,
      /backgroundColor:\s*agentUpTheme\.colors\.(?:surface|surfaceRaised|surfaceSelected|accent)\b/,
      `${file} restates a catalog fill from a color token`,
    );
  }
  const sidebar = sources.find(item => item.file.endsWith('WorkspaceSidebar.tsx'))?.source ?? '';
  const nav = sources.find(item => item.file.endsWith('AppNavBar.tsx'))?.source ?? '';
  const list = sources.find(item => item.file.endsWith('WorkspaceListScreen.tsx'))?.source ?? '';
  assert.match(sidebar, /auBox\('workspace'/);
  assert.match(sidebar, /auBox\('workspaceSelected'/);
  assert.match(nav, /auBox\('mobileBar'/);
  assert.match(list, /auBox\('workspace'/);
  assert.match(list, /auBox\('button'/);
});

test('HTML catalog enumerates every Agent-Up product surface', () => {
  const ids = catalog.surfaces.map(surface => surface.id);
  for (const id of ['foundations', 'primitives', 'chrome', 'workspaces', 'applications', 'browser', 'console', 'git', 'diagnostics', 'metrics', 'validation', 'auth', 'mobile', 'marketing', 'voice', 'brand', 'governance']) {
    assert.ok(ids.includes(id), `missing catalog surface ${id}`);
  }
  assert.ok(catalog.surfaces.every(surface => surface.components.length > 0));
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
