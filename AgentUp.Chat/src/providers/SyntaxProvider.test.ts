import assert from 'node:assert/strict';
import test from 'node:test';
import { highlightLineLimit, highlightLines, languageForFence, languageForPath } from './SyntaxProvider';

test('a fence tag names a grammar by id, alias or extension', () => {
  assert.equal(languageForFence('typescript'), 'typescript');
  assert.equal(languageForFence('bash'), 'shell');
  assert.equal(languageForFence('TSX'), 'typescript');
  assert.equal(languageForFence('yml'), 'yaml');
  assert.equal(languageForFence('brainfuck'), 'plaintext');
  assert.equal(languageForFence(undefined), 'plaintext');
  assert.equal(languageForPath('/repo/server.ts'), 'typescript');
});

test('code is highlighted per line unless it is plain or too long', () => {
  const lines = highlightLines('const port = 3001;\nreturn port;', 'typescript');
  assert.equal(lines?.length, 2);
  assert.ok(lines?.[0]?.some(token => token.kind === 'keyword' && token.text === 'const'));
  assert.ok(lines?.[0]?.some(token => token.kind === 'number'));
  assert.equal(highlightLines('plain', 'plaintext'), null);
  assert.equal(highlightLines(Array.from({ length: highlightLineLimit + 1 }, () => 'x').join('\n'), 'typescript'), null);
});
