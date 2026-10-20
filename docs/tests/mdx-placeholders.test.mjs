import assert from 'node:assert/strict';
import test from 'node:test';
import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';
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

test('Doc components do not evaluate raw route placeholders as JS', async () => {
  const files = [
    ...await markdownFiles(path.join(docsRoot, 'developer-guide')),
    ...await markdownFiles(path.join(docsRoot, 'user-docs')),
    ...await markdownFiles(path.join(docsRoot, 'api-reference')),
  ];
  const raw = /<(DocFact|DocContract|DocMeta)\b[^>]*>PUT \/api\/workspaces\/\{[a-zA-Z]+\}/;
  const offenders = [];
  for (const file of files) {
    const text = await readFile(file, 'utf8');
    if (raw.test(text)) offenders.push(path.relative(docsRoot, file));
  }
  assert.deepEqual(offenders, []);
});
