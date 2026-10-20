import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const docsRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = path.resolve(docsRoot, '..');

test('the API reference is its own versioned docs space', async () => {
  const config = await readFile(path.join(docsRoot, 'docusaurus.config.js'), 'utf8');
  const generator = await readFile(path.join(docsRoot, 'scripts', 'generate-api-reference.mjs'), 'utf8');
  assert.match(config, /id:\s*'apiReference'/);
  assert.match(config, /routeBasePath:\s*'\/api-reference'/);
  assert.match(config, /docsPluginId:\s*'apiReference'/);
  assert.match(config, /type:\s*'docsVersionDropdown'/);
  assert.match(generator, /apiReference_versioned_docs/);
  assert.match(generator, /apiReference_versions\.json/);
  assert.doesNotMatch(generator, /api-reference_versioned_docs/);
  const versions = JSON.parse(await readFile(path.join(docsRoot, 'apiReference_versions.json'), 'utf8'));
  assert.ok(versions.length >= 1, 'at least one previous API version must be kept');
  for (const version of versions) {
    const snapshot = path.join(docsRoot, 'apiReference_versioned_docs', `version-${version}`, 'index.md');
    const text = await readFile(snapshot, 'utf8');
    assert.match(text, /API Reference/);
    assert.match(text, new RegExp(`This snapshot is the ${version} HTTP contract`));
    assert.doesNotMatch(text, /unreleased Next contract/);
  }
});

test('generated API pages cover every Server HTTP controller route', async () => {
  const featuresRoot = path.join(repoRoot, 'AgentUp.Server', 'Features');
  const controllers = [];
  async function walk(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const full = path.join(directory, entry.name);
      if (entry.isDirectory()) await walk(full);
      else if (entry.name.endsWith('.cs') && !/Mcp|Mapping\.cs$/.test(entry.name)) controllers.push(full);
    }
  }
  await walk(featuresRoot);
  const spec = await readFile(path.join(docsRoot, 'api-reference', 'openapi.json'), 'utf8');
  const missing = [];
  for (const file of controllers) {
    const source = await readFile(file, 'utf8');
    const routes = [...source.matchAll(/Route\("([^"]+)"\)/g)].map(match => match[1]);
    const prefix = routes.find(route => route.startsWith('api/') || route.startsWith('apps/'));
    if (!prefix) continue;
    for (const match of source.matchAll(/\[Http(Get|Post|Put|Patch|Delete)(?:\(([^)]*)\))?\]/g)) {
      const template = (match[2] || '').match(/"([^"]*)"/)?.[1] || '';
      const pathValue = `/${prefix.replace(/^\/+|\/+$/g, '')}${template ? `/${template.replace(/^\/+/, '')}` : ''}`;
      if (!spec.includes(`"${pathValue}"`)) missing.push(`${path.basename(file)} ${match[1].toUpperCase()} ${pathValue}`);
    }
  }
  assert.deepEqual(missing, []);
});
