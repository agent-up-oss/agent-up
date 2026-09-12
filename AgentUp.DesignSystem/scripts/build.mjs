import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { emitCSharpColors, emitStyles, emitThemeResources } from './lib/avalonia.mjs';
import { catalogIndex, parseCatalog } from './lib/catalog.mjs';
import { parseCustomProperties, parseRules } from './lib/css.mjs';
import { emitNative } from './lib/native.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const check = process.argv.includes('--check');
const primitives = await readFile(resolve(root, 'src/agent-up.css'), 'utf8');
const product = await readFile(resolve(root, 'src/product.css'), 'utf8');
const marketing = await readFile(resolve(root, 'src/marketing.css'), 'utf8');
const catalogHtml = await readFile(resolve(root, 'src/catalog.html'), 'utf8');
const css = `${primitives.trim()}\n\n${product.trim()}\n`;
const tokens = parseCustomProperties(primitives);
const required = [
  'color-canvas', 'color-surface', 'color-border-subtle', 'color-text-primary',
  'color-accent', 'color-status-healthy', 'color-status-danger', 'color-focus',
  'space-1', 'space-4', 'radius-md', 'control-height', 'font-size-sm',
];
for (const name of required) {
  if (!tokens[name]) throw new Error(`Missing canonical CSS property --au-${name}.`);
}

const catalog = parseCatalog(catalogHtml);
if (catalog.surfaces.length < 8) throw new Error('Design catalog is missing required product surfaces.');
const rules = parseRules(`${css}\n${marketing}`);
const index = catalogIndex(catalog);
const native = emitNative(tokens, rules);
const wrappedCatalog = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Agent-Up design catalog</title>
  <link rel="stylesheet" href="./agent-up.css" />
  <link rel="stylesheet" href="./marketing.css" />
</head>
${catalogHtml.slice(catalogHtml.indexOf('<body'))}
`;

const outputs = new Map([
  ['dist/web/agent-up.css', css],
  ['dist/web/marketing.css', marketing],
  ['dist/web/catalog.html', wrappedCatalog],
  ['dist/web/catalog.json', `${JSON.stringify(catalog, null, 2)}\n`],
  ['dist/web/tokens.cjs', native.cjs],
  ['dist/native/index.js', native.js],
  ['dist/native/index.d.ts', native.dts],
  ['dist/avalonia/AgentUpTheme.axaml', emitThemeResources(tokens)],
  ['dist/avalonia/AgentUpStyles.axaml', emitStyles(rules, tokens, index)],
  ['dist/dotnet/AgentUpThemeColors.g.cs', emitCSharpColors(tokens)],
]);

for (const [relative, content] of outputs) {
  const path = resolve(root, relative);
  if (check) {
    let current = '';
    try { current = await readFile(path, 'utf8'); } catch { /* Report the missing output below. */ }
    if (current !== content) throw new Error(`${relative} is stale. Run npm run build in AgentUp.DesignSystem.`);
  } else {
    await mkdir(dirname(path), { recursive: true });
    await writeFile(path, content);
  }
}
