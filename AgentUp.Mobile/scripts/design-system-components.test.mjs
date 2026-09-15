import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { agentUpTheme } from '@agent-up/design-system/native';

const srcRoot = fileURLToPath(new URL('../src', import.meta.url));
const callPattern = /\bau(?:Box|Text)\(([^)]*)\)/g;
const namePattern = /'([^']+)'/g;

function walkSource(dir) {
  const files = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) files.push(...walkSource(path));
    else if (entry.name.endsWith('.ts') || entry.name.endsWith('.tsx')) files.push(path);
  }
  return files;
}

test('auBox and auText only reference catalog components', () => {
  const known = new Set(Object.keys(agentUpTheme.components));
  const unknown = [];

  for (const file of walkSource(srcRoot)) {
    const source = readFileSync(file, 'utf8');
    for (const match of source.matchAll(callPattern)) {
      for (const name of match[1].matchAll(namePattern)) {
        if (!known.has(name[1])) unknown.push(`${relative(srcRoot, file)}: ${name[1]}`);
      }
    }
  }

  assert.deepEqual(unknown, []);
});
