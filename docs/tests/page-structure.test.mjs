import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

const docsRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

async function markdownFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await markdownFiles(full));
    else if (entry.name.endsWith('.md') || entry.name.endsWith('.mdx')) files.push(full);
  }
  return files;
}

test('every authored docs page opens with DocWhat', async () => {
  const missing = [];
  for (const tree of ['user-docs', 'developer-guide']) {
    for (const file of await markdownFiles(path.join(docsRoot, tree))) {
      const text = await readFile(file, 'utf8');
      if (!text.includes('<DocWhat')) missing.push(path.relative(docsRoot, file));
    }
  }
  assert.deepEqual(missing, []);
});

test('facts and surfaces are not painted as the same card as the contract', async () => {
  const css = await readFile(new URL('../../AgentUp.DesignSystem/src/docs.css', import.meta.url), 'utf8');
  assert.match(css, /\.au-doc-contract \{[\s\S]*?background: var\(--au-color-surface\)/);
  const facts = css.match(/\.au-doc-facts,\s*\.au-doc-surfaces-wrap,\s*\.au-doc-fork \{[\s\S]*?\}/);
  assert.ok(facts, 'facts/surfaces share a definition-list rule');
  assert.match(facts[0], /border-block:/);
  assert.doesNotMatch(facts[0], /background:/);
  assert.doesNotMatch(facts[0], /border-radius:/);
});
