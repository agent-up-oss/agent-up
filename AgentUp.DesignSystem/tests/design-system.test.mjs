import assert from 'node:assert/strict';
import { readFile, readdir } from 'node:fs/promises';
import { extname, resolve } from 'node:path';
import test from 'node:test';
import { agentUpTheme, auBox, auText } from '../dist/native/index.js';
import catalog from '../dist/web/catalog.json' with { type: 'json' };
import {
  camel,
  layoutOnly,
  parseCustomProperties,
  parseRules,
  parseSelector,
  toPx,
  tokenKey,
  varName,
} from '../scripts/lib/css.mjs';

const root = new URL('..', import.meta.url);
const repository = resolve(new URL('../..', import.meta.url).pathname);
const visualProperties = new Set([
  'Background', 'Foreground', 'BorderBrush', 'BorderThickness', 'CornerRadius',
  'FontSize', 'FontWeight', 'FontFamily', 'MinHeight',
]);
const layoutCssProperties = new Set([
  'display', 'grid-template-columns', 'grid-template-rows', 'gap', 'row-gap', 'column-gap',
  'align-items', 'align-self', 'justify-content', 'justify-items', 'place-items',
  'padding', 'padding-block', 'padding-inline', 'padding-top', 'padding-right', 'padding-bottom', 'padding-left',
  'margin', 'margin-block', 'margin-inline', 'width', 'min-width', 'max-width', 'height', 'min-height', 'max-height',
  'overflow', 'overflow-x', 'overflow-y', 'cursor', 'text-align', 'font',
  '-webkit-box-orient', '-webkit-line-clamp', 'flex-direction', 'flex-wrap', 'flex',
]);

const primitives = await readFile(new URL('src/agent-up.css', root), 'utf8');
const product = await readFile(new URL('src/product.css', root), 'utf8');
const marketing = await readFile(new URL('src/marketing.css', root), 'utf8');
const tokens = parseCustomProperties(primitives);
const rules = parseRules(`${primitives}\n${product}\n${marketing}`);
const theme = await readFile(new URL('dist/avalonia/AgentUpTheme.axaml', root), 'utf8');
const avaloniaStyles = await readFile(new URL('dist/avalonia/AgentUpStyles.axaml', root), 'utf8');

test('native and Avalonia bindings round-trip every compileable token from canonical CSS', () => {
  const missingNative = [];
  const missingAvalonia = [];
  for (const [name, value] of Object.entries(tokens)) {
    if (name.startsWith('color-')) {
      const key = camel(name.slice('color-'.length));
      if (agentUpTheme.colors[key] !== value) missingNative.push(`${name} native=${agentUpTheme.colors[key]} css=${value}`);
      if (!theme.includes(`x:Key="${tokenKey(name)}"`)) missingAvalonia.push(name);
      continue;
    }
    const px = toPx(value);
    if (name.startsWith('space-') && px != null && agentUpTheme.spacing[name.slice('space-'.length)] !== px) {
      missingNative.push(name);
    }
    if (name.startsWith('radius-') && px != null && !value.includes('999') && agentUpTheme.radii[camel(name.slice('radius-'.length))] !== px) {
      missingNative.push(name);
    }
    if (name.startsWith('font-size-') && px != null && agentUpTheme.typography[camel(`size-${name.slice('font-size-'.length)}`)] !== px) {
      missingNative.push(name);
    }
    if (name.startsWith('control-height') && px != null && agentUpTheme.controls[camel(name.replace(/^control-/, ''))] !== px) {
      missingNative.push(name);
    }
    if (avaloniaEmits(name, value) && !theme.includes(`x:Key="${tokenKey(name)}"`)) missingAvalonia.push(name);
  }
  assert.deepEqual(missingNative, [], 'compiled native tokens drifted from canonical CSS');
  assert.deepEqual(missingAvalonia, [], 'Avalonia resources omitted compileable CSS tokens');
});

test('compileable class rules become native components and Avalonia selectors', () => {
  const missingNative = [];
  const missingAvalonia = [];
  for (const rule of rules) {
    for (const selector of rule.selectors) {
      const parsed = parseSelector(selector);
      if (!parsed || layoutOnly.has(parsed.baseClass)) continue;
      if (parsed.pseudo && parsed.pseudo !== 'disabled') continue;
      const className = parsed.modifierClass ?? parsed.baseClass;
      const visual = rule.declarations.some(([property]) =>
        /^(color|background|background-color|border|border-color|font-size|font-weight|opacity|min-height|padding)$/.test(property));
      if (!visual) continue;
      const baseName = className.replace(/^au-/, '').replaceAll('--', '-');
      const nativeName = camel(parsed.pseudo === 'disabled' ? `${baseName}-disabled` : baseName);
      if (!agentUpTheme.components[nativeName]) missingNative.push(selector);
      if (!avaloniaStyles.includes(`.${className}`)) missingAvalonia.push(selector);
    }
  }
  assert.deepEqual(missingNative, [], 'native bindings dropped compileable class rules');
  assert.deepEqual(missingAvalonia, [], 'Avalonia styles dropped compileable class rules');
});

test('catalog metadata is a complete, unique schema and Desktop aliases compile', () => {
  const surfaceIds = new Set();
  const componentIds = new Set();
  const missingAliases = [];
  assert.ok(catalog.surfaces.length > 0, 'catalog has no surfaces');
  for (const surface of catalog.surfaces) {
    assert.ok(surface.id && surface.title && surface.intro, `surface is missing schema: ${surface.id}`);
    assert.ok(!surfaceIds.has(surface.id), `duplicate surface id ${surface.id}`);
    surfaceIds.add(surface.id);
    assert.ok(surface.components.length > 0, `surface ${surface.id} has no examples`);
    for (const component of surface.components) {
      assert.ok(component.id && component.rootClass && component.html, `component is missing schema: ${surface.id}/${component.id}`);
      assert.ok(!componentIds.has(component.id), `duplicate component id ${component.id}`);
      componentIds.add(component.id);
      if (component.desktopClass && !avaloniaStyles.includes(`.${component.desktopClass}`)) {
        missingAliases.push(`${component.rootClass} → ${component.desktopClass}`);
      }
    }
  }
  assert.deepEqual(missingAliases, [], 'catalog Desktop aliases did not compile into Avalonia styles');
});

test('auBox and auText are total functions over the component catalog', () => {
  assert.throws(() => auBox('notACatalogComponent'));
  assert.throws(() => auText('notACatalogComponent'));
  const selected = auBox('workspace', 'workspaceSelected');
  assert.equal(selected.backgroundColor, agentUpTheme.components.workspaceSelected.backgroundColor);
  const name = auText('workspaceName', 'muted');
  assert.equal(name.color, agentUpTheme.components.muted.color);
});

test('compiler preserves CSS typography and disabled mappings rather than dropping them', () => {
  const letterSpaced = new Set();
  const transformed = new Set();
  const disabled = new Set();
  for (const rule of rules) {
    for (const selector of rule.selectors) {
      const parsed = parseSelector(selector);
      if (!parsed) continue;
      const className = parsed.modifierClass ?? parsed.baseClass;
      if (rule.declarations.some(([property]) => property === 'letter-spacing')) letterSpaced.add(className);
      if (rule.declarations.some(([property]) => property === 'text-transform')) transformed.add(className);
      if (parsed.pseudo === 'disabled') disabled.add(className);
    }
  }
  for (const className of letterSpaced) {
    const block = avaloniaStyles.match(new RegExp(`<Style Selector="TextBlock\\.${className}">([\\s\\S]*?)</Style>`));
    assert.ok(block, `${className} with letter-spacing did not compile to a TextBlock style`);
    assert.match(block[1], /Property="LetterSpacing"/, `${className} dropped letter-spacing`);
  }
  for (const className of transformed) {
    const nativeName = camel(className.replace(/^au-/, '').replaceAll('--', '-'));
    assert.ok(auText(nativeName).textTransform, `${className} dropped text-transform`);
  }
  for (const className of disabled) {
    const nativeName = camel(`${className.replace(/^au-/, '').replaceAll('--', '-')}-disabled`);
    assert.ok(agentUpTheme.components[nativeName], `${className}:disabled did not compile`);
  }
});

test('semantic roles survive compilation: action is not danger, selection is not an accent outline, surfaces sit on the canvas', () => {
  assert.equal(agentUpTheme.components.button.backgroundColor, agentUpTheme.colors.accent);
  assert.notEqual(agentUpTheme.components.button.backgroundColor, agentUpTheme.colors.statusDanger);
  assert.notEqual(agentUpTheme.components.cardSelected.borderColor, agentUpTheme.colors.accent);
  assert.notEqual(agentUpTheme.components.workspace.borderColor, agentUpTheme.colors.accent);
  assert.notEqual(agentUpTheme.colors.surface, agentUpTheme.colors.canvas);
  assert.notEqual(agentUpTheme.colors.surfaceRaised, agentUpTheme.colors.canvas);
  const buttonBackground = varName(
    rules.flatMap(rule => rule.selectors.includes('.au-button') ? rule.declarations : [])
      .find(([property]) => property === 'background')?.[1] ?? '',
  );
  assert.equal(buttonBackground, 'color-accent');
});

test('Desktop Window.Styles does not restate visual setters already emitted by AgentUpStyles', async () => {
  const axaml = await readFile(resolve(repository, 'AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml'), 'utf8');
  const local = parseStyleBlocks(axaml.slice(axaml.indexOf('<Window.Styles>'), axaml.indexOf('</Window.Styles>')));
  const generated = new Map(parseStyleBlocks(avaloniaStyles).map(block => [block.selector, new Set(block.properties)]));
  const restated = [];
  for (const block of local) {
    const emitted = generated.get(block.selector);
    if (!emitted) continue;
    for (const property of block.properties) {
      if (visualProperties.has(property) && emitted.has(property)) {
        restated.push(`${block.selector} ${property}`);
      }
    }
  }
  assert.deepEqual(restated, [], 'MainWindow restates generated visual setters');
});

test('Mobile does not rebuild catalog fills from color tokens', async () => {
  const fill = /backgroundColor:\s*agentUpTheme\.colors\.(?:surface|surfaceRaised|surfaceSelected|accent)\b/;
  const selectedBorder = /border(?:Top|Bottom|Left|Right)?Color:\s*agentUpTheme\.colors\.borderSelected/;
  for (const file of await filesUnder(resolve(repository, 'AgentUp.Mobile/src'), new Set(['.ts', '.tsx']))) {
    const source = await readFile(file, 'utf8');
    assert.doesNotMatch(source, fill, `${file} restates a catalog fill from a color token`);
    assert.doesNotMatch(source, selectedBorder, `${file} uses the selected border as default chrome`);
  }
});

test('showcase page CSS is layout-only; catalog classes own paint', async () => {
  const css = await readFile(resolve(repository, 'docs/src/pages/design-system/index.module.css'), 'utf8');
  const painted = [];
  for (const rule of parseRules(css)) {
    for (const [property] of rule.declarations) {
      if (!layoutCssProperties.has(property)) painted.push(`${rule.selectors.join(', ')} { ${property} }`);
    }
  }
  assert.deepEqual(painted, [], 'showcase module CSS paints instead of using catalog classes');
});

test('marketing CSS keeps the retired ambient neon treatment forbidden', () => {
  assert.doesNotMatch(marketing, /text-shadow|drop-shadow|radial-gradient/i);
});

test('brand voice is a closed lifecycle schema with required identity fields', async () => {
  const voice = JSON.parse(await readFile(new URL('brand/voice.json', root), 'utf8'));
  for (const field of ['productName', 'category', 'promise', 'boundary']) {
    assert.equal(typeof voice[field], 'string');
    assert.ok(voice[field].length > 0, `voice.${field} is empty`);
  }
  assert.deepEqual(Object.keys(voice.lifecycle), ['available', 'preview', 'experimental', 'planned']);
  for (const label of Object.values(voice.lifecycle)) {
    assert.equal(typeof label, 'string');
    assert.ok(label.length > 0);
  }
});

test('product consumers do not introduce a second palette', async () => {
  const roots = [
    resolve(repository, 'AgentUp.Desktop'),
    resolve(repository, 'AgentUp.Mobile/src'),
    resolve(repository, 'docs/src'),
  ];
  const extensions = new Set(['.axaml', '.cs', '.css', '.js', '.ts', '.tsx']);
  for (const dir of roots) {
    for (const file of await filesUnder(dir, extensions)) {
      const source = await readFile(file, 'utf8');
      assert.doesNotMatch(source, /#[0-9a-f]{3,8}\b|rgba?\s*\(/i, `${file} redefines a design-system color`);
    }
  }
});

test('installed Mobile chrome stays on the compiled canvas token', async () => {
  const manifest = JSON.parse(await readFile(resolve(repository, 'AgentUp.Mobile/public/manifest.json'), 'utf8'));
  const expo = JSON.parse(await readFile(resolve(repository, 'AgentUp.Mobile/app.json'), 'utf8'));
  assert.equal(manifest.background_color, agentUpTheme.colors.canvas);
  assert.equal(manifest.theme_color, agentUpTheme.colors.canvas);
  assert.equal(expo.expo.userInterfaceStyle, 'dark');
});

function avaloniaEmits(name, value) {
  if (name.startsWith('color-') || name.startsWith('weight-') || name.startsWith('line-')) return true;
  if (name === 'font-sans' || name === 'font-mono') return true;
  if (name.startsWith('radius-') && value.includes('999')) return false;
  const px = toPx(value);
  if (px == null) return false;
  return name.startsWith('space-')
    || name.startsWith('radius-')
    || name.startsWith('font-size-')
    || name.startsWith('control-height')
    || name === 'stroke'
    || name === 'stroke-strong';
}
  return [...axaml.matchAll(/<Style Selector="([^"]+)">([\s\S]*?)<\/Style>/g)].map(([, selector, body]) => ({
    selector,
    properties: [...body.matchAll(/Property="([^"]+)"/g)].map(match => match[1]),
  }));
}

async function filesUnder(directory, extensions) {
  const entries = await readdir(directory, { withFileTypes: true });
  const nested = await Promise.all(entries
    .filter(entry => !['bin', 'obj', 'node_modules', 'build', 'dist'].includes(entry.name))
    .map(entry => entry.isDirectory()
      ? filesUnder(resolve(directory, entry.name), extensions)
      : extensions.has(extname(entry.name)) && !/\.test\.[^.]+$/.test(entry.name)
        ? [resolve(directory, entry.name)]
        : []));
  return nested.flat();
}
