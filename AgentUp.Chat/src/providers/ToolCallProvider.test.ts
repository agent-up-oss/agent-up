import assert from 'node:assert/strict';
import test from 'node:test';
import { durationBetween, formatOutput, minimumFold, presentToolCall, presentToolFacts, previewLines, shorten } from './ToolCallProvider';

test('an Agent-Up MCP tool reads as its action with the Agent-Up source', () => {
  const view = presentToolCall('agent-up-orchestration_list_workspaces', 'other', undefined, undefined, 'pending');
  assert.deepEqual([view.title, view.source, view.summary, view.state], ['List workspaces', 'Agent-Up', 'orchestration', 'running']);
});

test('another MCP server is named as the source', () => {
  const view = presentToolCall('github-api_searchIssues', 'other', '{"query":"is:open label:bug"}', undefined, 'completed');
  assert.deepEqual([view.title, view.source, view.summary], ['Search issues', 'Github api', 'is:open label:bug']);
});

test("the agent's own tool is named by its kind with the argument that matters", () => {
  const edit = presentToolCall('/repo/src/app.ts', 'edit', undefined, undefined, 'completed');
  const run = presentToolCall('bash', 'execute', '{"command":"npm test","description":"Run the tests"}', undefined, 'failed');
  assert.deepEqual([edit.title, edit.summary, edit.source], ['Edit', '/repo/src/app.ts', undefined]);
  assert.deepEqual([run.title, run.summary, run.state], ['Run', 'npm test', 'failed']);
});

test('json output is indented and long output is folded behind a count', () => {
  const lines = Array.from({ length: 20 }, (_, index) => `line ${index + 1}`).join('\n');
  const json = presentToolCall('x-y_z', undefined, '{"a":1}', '{"state":"Running","ports":[3000]}', 'completed');
  const long = presentToolCall('x-y_z', undefined, undefined, lines, 'completed');
  assert.match(json.output ?? '', /\n {2}"state": "Running"/);
  assert.match(json.input ?? '', /"a": 1/);
  assert.equal(long.hiddenLines, 20 - previewLines);
  assert.equal(long.output?.split('\n').length, previewLines);
  assert.equal(long.fullOutput, lines);
});

test('a long summary keeps both ends on one line, and non-json output is kept as written', () => {
  const shortened = shorten(`/home/user/projects/${'a'.repeat(100)}/file.ts`);
  assert.ok(shortened.length <= 72);
  assert.ok(shortened.startsWith('/home/user/projects/') && shortened.endsWith('/file.ts') && shortened.includes('…'));
  assert.equal(formatOutput('{not json'), '{not json');
  assert.equal(formatOutput('  '), undefined);
  assert.equal(presentToolCall(undefined, undefined, '[1]').title, 'Tool');
});

test('a few lines of overflow are shown rather than folded', () => {
  const lines = Array.from({ length: previewLines + minimumFold - 1 }, (_, index) => `line ${index + 1}`).join('\n');
  const view = presentToolCall('x-y_z', undefined, undefined, lines, 'completed');
  assert.equal(view.hiddenLines, 0);
  assert.equal(view.output, lines);
});

test('durations under a second are not worth showing', () => {
  const start = '2026-01-01T00:00:00.000Z';
  assert.equal(durationBetween(start, '2026-01-01T00:00:00.400Z'), undefined);
  assert.equal(durationBetween(start, '2026-01-01T00:00:04.600Z'), '5s');
  assert.equal(durationBetween(start, '2026-01-01T00:02:05.000Z'), '2m 5s');
  assert.equal(durationBetween(undefined, start), undefined);
});

test('a write is named a write and shows its content as a new file', () => {
  const view = presentToolFacts({ name: 'write', title: 'notes.md', kind: 'edit', rawInput: '{"filePath":"/repo/notes.md","content":"a\\nb"}', output: 'Wrote file successfully.', status: 'completed' });
  assert.equal(view.title, 'Write');
  assert.equal(view.summary, '/repo/notes.md');
  assert.deepEqual([view.diff?.added, view.diff?.removed], [2, 0]);
});

test('an edit prefers the unified diff over the replaced snippet', () => {
  const unified = presentToolFacts({ name: 'edit', kind: 'edit', status: 'completed', unifiedDiff: '@@ -1,1 +1,1 @@\n-listen(3000)\n+listen(3001)', diffOldText: '3000', diffNewText: '3001' });
  const snippet = presentToolFacts({ name: 'edit', kind: 'edit', status: 'completed', diffPath: '/repo/server.ts', diffOldText: '3000', diffNewText: '3001' });
  assert.equal(unified.diff?.lines.find(line => line.kind === 'added')?.text, 'listen(3001)');
  assert.equal(snippet.diff?.path, '/repo/server.ts');
  assert.deepEqual(snippet.diff?.lines.map(line => line.text), ['3000', '3001']);
});

test('a read with empty visible content shows its raw output', () => {
  const view = presentToolFacts({ name: 'read', kind: 'read', status: 'completed', output: '', rawOutput: 'file text' });
  assert.equal(view.output, 'file text');
});
