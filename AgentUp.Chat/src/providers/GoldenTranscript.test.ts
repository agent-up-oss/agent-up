import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import type { AgentEvent, TranscriptItem } from '../models/AgentSession';
import { applyPresentedUpdate, endTurn, groupTranscript, groupWork, presentSessionUpdate, runSummary, unwrapSessionUpdate, userMessageItem } from './AgentEventPresentationProvider';
import { parseMarkdown } from './MarkdownProvider';
import { presentToolFacts } from './ToolCallProvider';

/**
 * Replays the event stream a real OpenCode session against the CPU model produced - write, read,
 * edit, run, search, an Agent-Up MCP call and a markdown answer - through the same reducers the
 * chat screen uses. Desktop replays the same file.
 */
function replay(): TranscriptItem[] {
  const events = readFileSync(new URL('../../fixtures/opencode-cpu-session.jsonl', import.meta.url), 'utf8')
    .split('\n').filter(Boolean).map(line => JSON.parse(line) as AgentEvent);
  return events.reduce<TranscriptItem[]>((items, event) => {
    if (event.type === 'user_message') return [...items, userMessageItem(event.sequence, event.payload, event.timestamp)];
    if (event.type === 'prompt_result') return endTurn(items, event.timestamp);
    if (event.type !== 'session_update') return items;
    return applyPresentedUpdate(items, `${event.sequence}`, presentSessionUpdate(unwrapSessionUpdate(event.payload)), event.timestamp);
  }, []);
}

const tools = (items: TranscriptItem[]) => items.filter(item => item.role === 'tool').map(item => presentToolFacts({ ...item, kind: item.toolKind }));

test('every tool of the real session reads as its action', () => {
  const views = tools(replay());
  assert.deepEqual(views.map(view => view.title), ['Write', 'Read', 'Edit', 'Run', 'Search', 'List workspaces']);
  assert.ok(views.every(view => view.state === 'done'));
  assert.equal(views.at(-1)?.source, 'Agent-Up');
  assert.equal(views[3]?.summary, 'ls -la');
  assert.match(views[1]?.fullOutput ?? '', /^\(Showing lines 1-0 of 1/);
  assert.equal(views[4]?.summary, '*.md');
});

test('the write and the edit are shown as diffs', () => {
  const [write, , edit] = tools(replay());
  assert.ok((write?.diff?.added ?? 0) >= 3);
  assert.equal(write?.diff?.removed, 0);
  assert.deepEqual([edit?.diff?.added, edit?.diff?.removed], [1, 1]);
  assert.match(edit?.diff?.lines.find(line => line.kind === 'added')?.text ?? '', /3001/);
  assert.match(edit?.diff?.lines.find(line => line.kind === 'deleted')?.text ?? '', /3000/);
});

test('the markdown answer draws its heading and table rather than a code block', () => {
  const blocks = groupTranscript(replay());
  const reply = blocks.at(-1);
  assert.equal(reply?.type, 'reply');
  const kinds = parseMarkdown(reply?.type === 'reply' ? reply.item.text : '').map(block => block.kind);
  assert.ok(kinds.includes('heading'));
  assert.ok(kinds.includes('table'));
  assert.ok(!kinds.includes('code'));
});

test('each turn reports how long it worked, from its prompt', () => {
  const runs = groupTranscript(replay()).flatMap(block => block.type === 'run' ? [block] : []);
  assert.equal(runs.length, 6);
  assert.equal(runSummary(runs[0]!.items, runs[0]!.duration), 'Worked for 57s · 1 tool');
  assert.ok(runs.every(run => /^Worked for \d+s · 1 tool$/.test(runSummary(run.items, run.duration))));
  assert.ok(runs.every(run => groupWork(run.items, () => undefined).every(entry => entry.type === 'item')), 'One call per turn folds nothing.');
});
