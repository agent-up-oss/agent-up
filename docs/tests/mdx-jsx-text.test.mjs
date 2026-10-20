import assert from 'node:assert/strict';
import { readdir, readFile } from 'node:fs/promises';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const docsRoot = fileURLToPath(new URL('..', import.meta.url));
const jsxComponent = /<(DocFact|DocContract|DocWhat|DocBeat|DocSurface|DocStep|DocFocus|DocCallout|DocNext|DocEyebrow)\b[^>]*>([\s\S]*?)<\/\1>/g;
const unquotedIdent = /\{[A-Za-z_][A-Za-z0-9_]*\}/;

async function* markdownFiles(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      yield* markdownFiles(path);
      continue;
    }

    if (entry.name.endsWith('.md') || entry.name.endsWith('.mdx')) {
      yield path;
    }
  }
}

function hasUnquotedIdent(inner) {
  const withoutLiterals = inner
    .replace(/```[\s\S]*?```/g, '')
    .replace(/`[^`]*`/g, '')
    .replace(/\{['"][\s\S]*?['"]\}/g, '');

  return unquotedIdent.test(withoutLiterals);
}

test('JSX children quote {id} templates so MDX does not evaluate them', async () => {
  const failures = [];
  for (const tree of ['user-docs', 'developer-guide', 'api-reference']) {
    for await (const path of markdownFiles(join(docsRoot, tree))) {
      const source = await readFile(path, 'utf8');
      for (const match of source.matchAll(jsxComponent)) {
        if (hasUnquotedIdent(match[2])) {
          failures.push(`${relative(docsRoot, path)}: ${match[0].split('\n')[0]}`);
        }
      }
    }
  }

  assert.deepEqual(failures, []);
});
