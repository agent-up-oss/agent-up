import assert from 'node:assert/strict';
import test from 'node:test';
import { blockKey, parseInlines, parseMarkdown, type MarkdownBlock } from './MarkdownProvider';

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

test('parseMarkdown reads a table with its header, alignments, padded rows and escaped pipes', () => {
  const blocks = parseMarkdown('Ports:\n| Name | Port | Note |\n|:---|---:|:---:|\n| web | 3000 | a \\| b |\n| api |\n\nAfter.');
  assert.deepEqual(blocks.map(block => block.kind), ['paragraph', 'table', 'paragraph']);
  const table = blocks[1];
  assert.ok(table?.kind === 'table');
  assert.deepEqual(table.alignments, ['left', 'right', 'center']);
  assert.ok(table.rows.every(row => row.length === 3));
  assert.equal(table.rows[1]?.[2]?.map(inline => inline.text).join(''), 'a | b');
});

test('a reply wrapped whole in a markdown fence is drawn as its markdown', () => {
  assert.deepEqual(parseMarkdown('```markdown\n# Title\n\n| A | B |\n|---|---|\n| 1 | 2 |\n```').map(block => block.kind), ['heading', 'table']);
  assert.deepEqual(parseMarkdown('```md\n# Title\n\nStill wri').map(block => block.kind), ['heading', 'paragraph']);
  assert.equal(parseMarkdown('Here is markdown:\n```markdown\n# Title\n```')[1]?.kind, 'code');
  assert.deepEqual(parseMarkdown('```markdown\n# A\n```\n\n```markdown\n# B\n```').map(block => block.kind), ['code', 'code']);
});

test('block keys change only when what the block draws changes', () => {
  const before = parseMarkdown('# T\n\nOne');
  const after = parseMarkdown('# T\n\nOne two');
  assert.equal(blockKey(after[0]!), blockKey(before[0]!));
  assert.notEqual(blockKey(after[1]!), blockKey(before[1]!));
});
