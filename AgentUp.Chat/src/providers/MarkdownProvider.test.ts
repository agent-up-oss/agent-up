import assert from 'node:assert/strict';
import test from 'node:test';
import { parseInlines, parseMarkdown, type MarkdownBlock } from './MarkdownProvider';

const plain = (block: MarkdownBlock) => 'inlines' in block ? block.inlines.map(inline => inline.text).join('') : '';

test('parseMarkdown reads headings, paragraphs, lists, quotes and rules', () => {
  const blocks = parseMarkdown('## Workspaces\nOne line\nthat continues.\n\n- first\n  still first\n2. second\n> quoted\n---');
  assert.deepEqual(blocks.map(block => block.kind), ['heading', 'paragraph', 'listItem', 'listItem', 'quote', 'rule']);
  assert.equal(plain(blocks[1]), 'One line that continues.');
  assert.equal(plain(blocks[2]), 'first still first');
  assert.equal(blocks[3].kind === 'listItem' && blocks[3].marker, '2.');
});

test('parseMarkdown keeps fenced code verbatim and draws an unclosed fence as code so far', () => {
  const closed = parseMarkdown('```json\n{ "a": 1 }\n```\nafter');
  const streaming = parseMarkdown('Look:\n```bash\nnpm test');
  assert.deepEqual(closed[0], { kind: 'code', code: '{ "a": 1 }', language: 'json' });
  assert.equal(plain(closed[1]), 'after');
  assert.deepEqual(streaming.at(-1), { kind: 'code', code: 'npm test', language: 'bash' });
});

test('parseMarkdown marks nested list depth', () => {
  const blocks = parseMarkdown('- top\n  - nested\n    1) deeper');
  assert.deepEqual(blocks.map(block => block.kind === 'listItem' ? [block.depth, block.marker] : null), [[0, '•'], [1, '•'], [2, '1.']]);
});

test('parseInlines reads code, bold, italic and links', () => {
  const inlines = parseInlines('Run `npm test` on **main** with *care*, see [docs](https://example.test).');
  assert.deepEqual(inlines.map(inline => inline.kind), ['text', 'code', 'text', 'bold', 'text', 'italic', 'text', 'link', 'text']);
  assert.equal(inlines[1].text, 'npm test');
  assert.equal(inlines[7].url, 'https://example.test');
});

test('parseInlines leaves snake_case and unmatched markers as text', () => {
  assert.deepEqual(parseInlines('call list_workspaces and get_workspace_status'), [{ kind: 'text', text: 'call list_workspaces and get_workspace_status' }]);
  assert.deepEqual(parseInlines('2 * 3 and **open'), [{ kind: 'text', text: '2 * 3 and **open' }]);
  assert.deepEqual(parseMarkdown('  \n '), []);
});
