import assert from 'node:assert/strict';
import test from 'node:test';
import type { TranscriptItem } from '../models/AgentSession';
import { draggedHeight, opensInSheet, presentSheet, settleSheet, sheetDetents, sheetTakesDrag, sheetTopGap } from './SheetProvider';

const detents = { half: 400, full: 760 };

test('a sheet opens to half the window and fills it below the status bar', () => {
  const sized = sheetDetents(800, 28);
  assert.deepEqual(sized, { half: 400, full: 800 - 28 - sheetTopGap });
});

test('half never exceeds full on a short window', () => {
  const sized = sheetDetents(100, 60);
  assert.ok(sized.half <= sized.full);
});

test('at half height any vertical drag moves the sheet', () => {
  assert.equal(sheetTakesDrag({ detent: 'half', scrollTop: 0, dx: 0, dy: -20 }), true);
  assert.equal(sheetTakesDrag({ detent: 'half', scrollTop: 0, dx: 0, dy: 20 }), true);
});

test('at full height the content scrolls until it is back at its top', () => {
  assert.equal(sheetTakesDrag({ detent: 'full', scrollTop: 0, dx: 0, dy: -20 }), false);
  assert.equal(sheetTakesDrag({ detent: 'full', scrollTop: 120, dx: 0, dy: 20 }), false);
  assert.equal(sheetTakesDrag({ detent: 'full', scrollTop: 0, dx: 0, dy: 20 }), true);
});

test('a sideways move or a tap stays with the content', () => {
  assert.equal(sheetTakesDrag({ detent: 'half', scrollTop: 0, dx: 40, dy: 12 }), false);
  assert.equal(sheetTakesDrag({ detent: 'half', scrollTop: 0, dx: 0, dy: 3 }), false);
});

test('the sheet follows the finger and resists past full height', () => {
  assert.equal(draggedHeight(400, -100, detents), 500);
  assert.equal(draggedHeight(400, 500, detents), 0);
  assert.equal(draggedHeight(760, -100, detents), 780);
});

test('a slow release settles at the nearest rest', () => {
  assert.equal(settleSheet({ height: 650, velocity: 0, from: 'half', detents }), 'full');
  assert.equal(settleSheet({ height: 420, velocity: 0, from: 'full', detents }), 'half');
  assert.equal(settleSheet({ height: 150, velocity: 0, from: 'half', detents }), 'closed');
});

test('a flick goes one detent in its direction', () => {
  assert.equal(settleSheet({ height: 380, velocity: 1.5, from: 'half', detents }), 'closed');
  assert.equal(settleSheet({ height: 700, velocity: 1.5, from: 'full', detents }), 'half');
  assert.equal(settleSheet({ height: 420, velocity: -1.5, from: 'half', detents }), 'full');
});

test('a flick down from full that already passed half closes', () => {
  assert.equal(settleSheet({ height: 300, velocity: 1.5, from: 'full', detents }), 'closed');
});

test('only tool calls and thoughts open into a sheet', () => {
  assert.deepEqual((['tool', 'thought', 'plan', 'agent', 'user'] as const).map(role => opensInSheet({ role })), [true, true, false, false, false]);
});

test('a thought is titled by how long it took and needs no status', () => {
  const thought: TranscriptItem = { id: 't', role: 'thought', text: 'hm', startedAt: '2026-01-01T00:00:00Z', endedAt: '2026-01-01T00:00:03Z' };
  assert.equal(presentSheet(thought, true).title, 'Thinking');
  assert.deepEqual(presentSheet(thought, false), { title: 'Thought for 3s', arguments: [] });
});

test('an edit is titled with its file and leaves the path and counts to its diff', () => {
  const edit: TranscriptItem = {
    id: 'e', role: 'tool', text: '', toolKind: 'edit', status: 'completed', title: 'edit',
    rawInput: JSON.stringify({ filePath: 'src/api/products.ts', oldString: 'a', newString: 'b' }),
    diffPath: 'src/api/products.ts', diffOldText: 'a', diffNewText: 'b',
    startedAt: '2026-01-01T00:00:00Z', endedAt: '2026-01-01T00:00:02Z',
  };
  assert.deepEqual(presentSheet(edit, false), { title: 'Edit products.ts', status: { tone: 'done', text: '✓ Done · 2s' }, arguments: [] });
});

test('a read keeps its whole path as a detail', () => {
  const read: TranscriptItem = { id: 'r', role: 'tool', text: '', toolKind: 'read', status: 'completed', title: 'read', rawInput: JSON.stringify({ filePath: 'src/api/products.ts' }) };
  assert.deepEqual(presentSheet(read, false).arguments, [{ label: 'Path', value: 'src/api/products.ts' }]);
});

test('a failed command reads as a sentence with its other arguments as labelled values, not JSON', () => {
  const run: TranscriptItem = {
    id: 'r', role: 'tool', text: '', toolKind: 'execute', status: 'failed', title: 'bash',
    rawInput: JSON.stringify({ command: 'npm test', description: 'Run the tests', timeout: 120 }),
    startedAt: '2026-01-01T00:00:00Z', endedAt: '2026-01-01T00:00:04Z',
  };
  assert.deepEqual(presentSheet(run, false), {
    title: 'Run npm test',
    status: { tone: 'failed', text: '✕ Failed · 4s' },
    arguments: [{ label: 'Description', value: 'Run the tests' }, { label: 'Timeout', value: '120' }],
  });
});

test('a command too long for the title keeps its whole text as a detail', () => {
  const command = `npx vitest run ${'src/storefront/'.repeat(6)}Featured.test.tsx`;
  const run: TranscriptItem = { id: 'l', role: 'tool', text: '', toolKind: 'execute', status: 'completed', title: 'bash', rawInput: JSON.stringify({ command }) };
  assert.deepEqual(presentSheet(run, false).arguments, [{ label: 'Command', value: command }]);
});

test('an argument longer than the old cut-off stays whole', () => {
  const body = 'x'.repeat(900);
  const call: TranscriptItem = { id: 'b', role: 'tool', text: '', status: 'completed', title: 'github-api_createIssue', rawInput: JSON.stringify({ title: 'Banner', body }) };
  assert.equal(presentSheet(call, false).arguments.find(row => row.label === 'Body')?.value, body);
});

test('a path too long for the title is a detail in full, not its shortened form', () => {
  const filePath = `src/${'storefront/'.repeat(8)}WeeklySpecial.tsx`;
  const read: TranscriptItem = { id: 'p', role: 'tool', text: '', toolKind: 'read', status: 'completed', title: 'read', rawInput: JSON.stringify({ filePath }) };
  const sheet = presentSheet(read, false);
  assert.equal(sheet.title, 'Read WeeklySpecial.tsx');
  assert.deepEqual(sheet.arguments, [{ label: 'File path', value: filePath }]);
});

test('a running MCP tool names its source and leaves out the argument the title already says', () => {
  const tool: TranscriptItem = { id: 'm', role: 'tool', text: '', status: 'pending', title: 'github-api_searchIssues', rawInput: JSON.stringify({ query: 'is:open', perPage: 5 }) };
  assert.deepEqual(presentSheet(tool, false), {
    title: 'Search issues is:open',
    status: { tone: 'running', text: 'Running · Github api' },
    arguments: [{ label: 'Per page', value: '5' }],
  });
});
