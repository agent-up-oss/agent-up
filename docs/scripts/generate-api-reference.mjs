#!/usr/bin/env node
import { mkdir, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const docsRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repoRoot = resolve(docsRoot, '..');
const serverFeatures = join(repoRoot, 'AgentUp.Server', 'Features');
const currentRoot = join(docsRoot, 'api-reference');
const check = process.argv.includes('--check');
const snapshotIndex = process.argv.indexOf('--snapshot');
const snapshotVersion = snapshotIndex >= 0 ? process.argv[snapshotIndex + 1] : null;

const httpVerb = /\[Http(Get|Post|Put|Patch|Delete)(?:\(([^)]*)\))?\]/g;
const routeAttr = /Route\("([^"]+)"\)/g;
const skipName = /Mcp|Mapping\.cs$/;

function titleCase(value) {
  return value.replace(/([a-z])([A-Z])/g, '$1 $2');
}

function unwrapArg(raw) {
  if (raw == null) return '';
  const quoted = raw.match(/"([^"]*)"/);
  return quoted ? quoted[1] : raw.trim().replace(/^Name\s*=\s*"[^"]*"\s*,?\s*/, '');
}

function joinRoute(prefix, suffix) {
  const left = prefix.replace(/\/+$/, '');
  const right = String(suffix || '').replace(/^\/+/, '');
  if (!right) return `/${left.replace(/^\/+/, '')}`;
  return `/${left.replace(/^\/+/, '')}/${right}`;
}

function featureFromPath(file) {
  const parts = relative(serverFeatures, file).split('/');
  return parts[0];
}

function slug(feature) {
  return feature.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase();
}

async function csharpFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await csharpFiles(full));
    else if (entry.name.endsWith('.cs') && !skipName.test(entry.name)) files.push(full);
  }
  return files;
}

function parseController(source, file) {
  const classMatch = source.match(/class\s+(\w+)/);
  if (!classMatch) return [];
  const routes = [...source.matchAll(routeAttr)].map(match => match[1]);
  const prefix = routes.find(route => !route.includes('**') && route !== '') ?? routes[0];
  if (!prefix || prefix.startsWith('{')) return [];
  if (!prefix.startsWith('api/') && !prefix.startsWith('apps/')) return [];

  const operations = [];
  for (const match of source.matchAll(httpVerb)) {
    const method = match[1].toUpperCase();
    const template = unwrapArg(match[2]);
    const after = source.slice(match.index, match.index + 400);
    const nameMatch = after.match(/(?:public|internal|protected|private).+?\s+(\w+)\s*\(/);
    operations.push({
      method,
      path: joinRoute(prefix, template),
      operationId: nameMatch?.[1] ?? `${method}_${template || 'root'}`,
      controller: classMatch[1],
      feature: featureFromPath(file),
    });
  }
  return operations;
}

function renderIndex(features, versionLabel) {
  const preferred = ['Workspaces', 'Applications', 'Agents'];
  const names = features.map(feature => feature.name);
  const beatNames = preferred.filter(name => names.includes(name));
  while (beatNames.length < 3 && beatNames.length < names.length) {
    const next = names.find(name => !beatNames.includes(name));
    if (!next) break;
    beatNames.push(next);
  }
  const beats = beatNames.map(name => `  <DocBeat>${titleCase(name)}</DocBeat>`).join('\n');
  const list = features.map(feature => `- [${titleCase(feature.name)}](./${feature.slug})`).join('\n');
  const snapshot = versionLabel !== 'Next';
  const what = snapshot
    ? `This snapshot is the ${versionLabel} HTTP contract. Newer work lives on Next.`
    : 'Previous release snapshots stay on this site. The current pages are the unreleased Next contract.';
  return `---
title: API Reference
slug: /
---

# API Reference

<DocWhat>
This space is the Server HTTP contract. Each feature page lists the routes that feature owns.

${what}
</DocWhat>

<DocSpine>
${beats}
</DocSpine>

<DocContract label="Current">${versionLabel}</DocContract>

<DocFacts label="How to read it">
<DocFact label="Source">AgentUp.Server feature controllers</DocFact>
<DocFact label="Generate">docs/scripts/generate-api-reference.mjs</DocFact>
<DocFact label="Snapshot">{'node docs/scripts/generate-api-reference.mjs --snapshot 3.61.0'}</DocFact>
</DocFacts>

## Features

${list}
`;
}

function renderFeature(feature) {
  const rows = feature.operations.map(op =>
    `| \`${op.method}\` | \`${op.path}\` | ${op.operationId} |`).join('\n');
  return `---
title: ${titleCase(feature.name)}
---

# ${titleCase(feature.name)}

<DocWhat>
HTTP routes owned by the ${titleCase(feature.name)} slice.
</DocWhat>

<DocContract label="Prefix">${feature.prefix}</DocContract>

| Method | Path | Operation |
|---|---|---|
${rows}
`;
}

function renderOpenApi(operations) {
  const paths = {};
  for (const op of operations) {
    const item = paths[op.path] ?? (paths[op.path] = {});
    item[op.method.toLowerCase()] = {
      tags: [op.feature],
      operationId: `${op.controller}_${op.operationId}`,
      responses: { 200: { description: 'Success' } },
    };
  }
  return {
    openapi: '3.0.3',
    info: {
      title: 'Agent-Up Server HTTP API',
      description: 'Generated from AgentUp.Server feature controllers.',
      version: 'next',
    },
    paths,
  };
}

function sidebar(features) {
  return {
    apiReferenceSidebar: [
      'index',
      {
        type: 'category',
        label: 'HTTP',
        collapsed: false,
        items: features.map(feature => feature.slug),
      },
    ],
  };
}

async function collectOperations() {
  const files = await csharpFiles(serverFeatures);
  const operations = [];
  for (const file of files) {
    const source = await readFile(file, 'utf8');
    operations.push(...parseController(source, file));
  }
  operations.sort((a, b) => a.path.localeCompare(b.path) || a.method.localeCompare(b.method));
  return operations;
}

function groupFeatures(operations) {
  const byFeature = new Map();
  for (const op of operations) {
    const entry = byFeature.get(op.feature) ?? { name: op.feature, slug: slug(op.feature), operations: [] };
    entry.operations.push(op);
    byFeature.set(op.feature, entry);
  }
  const features = [...byFeature.values()].sort((a, b) => a.name.localeCompare(b.name));
  for (const feature of features) {
    const prefixes = [...new Set(feature.operations.map(op =>
      op.path.split('/').slice(0, 3).join('/')))];
    feature.prefix = prefixes[0] ?? '/api';
  }
  return features;
}

async function writeTree(root, features, operations, versionLabel) {
  await mkdir(root, { recursive: true });
  const files = new Map([
    ['index.md', renderIndex(features, versionLabel)],
    ['openapi.json', `${JSON.stringify(renderOpenApi(operations), null, 2)}\n`],
  ]);
  for (const feature of features) {
    files.set(`${feature.slug}.md`, renderFeature(feature));
  }
  for (const [name, contents] of files) {
    await writeFile(join(root, name), contents);
  }
  return files;
}

async function snapshot(version, features) {
  // Docusaurus versions paths are keyed by plugin id, not route path.
  const dest = join(docsRoot, 'apiReference_versioned_docs', `version-${version}`);
  const sidebarsDest = join(docsRoot, 'apiReference_versioned_sidebars', `version-${version}-sidebars.json`);
  await rm(dest, { recursive: true, force: true });
  await mkdir(dest, { recursive: true });
  await mkdir(dirname(sidebarsDest), { recursive: true });
  const names = ['index.md', 'openapi.json', ...features.map(feature => `${feature.slug}.md`)];
  for (const name of names) {
    const source = name === 'index.md'
      ? renderIndex(features, version)
      : await readFile(join(currentRoot, name), 'utf8');
    await writeFile(join(dest, name), source);
  }
  await writeFile(sidebarsDest, `${JSON.stringify(sidebar(features), null, 2)}\n`);
  const versionsPath = join(docsRoot, 'apiReference_versions.json');
  const versions = JSON.parse(await readFile(versionsPath, 'utf8').catch(() => '[]'));
  if (!versions.includes(version)) versions.unshift(version);
  await writeFile(versionsPath, `${JSON.stringify(versions, null, 2)}\n`);
}

const operations = await collectOperations();
if (operations.length === 0) {
  throw new Error('No Server HTTP routes were found for the API reference.');
}
const features = groupFeatures(operations);
const versionLabel = 'Next';
const planned = new Map();
planned.set('index.md', renderIndex(features, versionLabel));
planned.set('openapi.json', `${JSON.stringify(renderOpenApi(operations), null, 2)}\n`);
for (const feature of features) planned.set(`${feature.slug}.md`, renderFeature(feature));

if (check) {
  for (const [name, contents] of planned) {
    const existing = await readFile(join(currentRoot, name), 'utf8').catch(() => '');
    if (existing !== contents) {
      throw new Error(`API reference is stale: ${name}. Run node docs/scripts/generate-api-reference.mjs`);
    }
  }
} else {
  await rm(currentRoot, { recursive: true, force: true });
  await writeTree(currentRoot, features, operations, versionLabel);
}

  await mkdir(join(docsRoot, 'static', 'api'), { recursive: true });
  await writeFile(join(docsRoot, 'static', 'api', 'openapi.json'), planned.get('openapi.json'));
  await writeFile(join(docsRoot, 'sidebarsApi.json'), `${JSON.stringify(sidebar(features), null, 2)}\n`);

if (snapshotVersion) {
  if (!/^\d+\.\d+\.\d+$/.test(snapshotVersion)) {
    throw new Error(`Snapshot version must be x.y.z, got ${snapshotVersion}`);
  }
  await snapshot(snapshotVersion, features);
}
