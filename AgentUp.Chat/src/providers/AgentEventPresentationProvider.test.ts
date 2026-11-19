import assert from 'node:assert/strict';
import test from 'node:test';
import {
  activityHint,
  afterPermissionDecision,
  agentEventRole,
  applyPresentedUpdate,
  groupTranscript,
  liveRunId,
  parsePermission,
  permissionOptionLabel,
  permissionOptionTone,
  presentSessionUpdate,
  resolveActivity,
  steeringStatus,
  userMessageItem,
  runSummary,
  unwrapSessionUpdate,
  visibleText,
  mergeAgentSession,
  groupWork,
  thoughtLabel,
  isFollowing,
  endTurn,
} from './AgentEventPresentationProvider';
import type { TranscriptItem, AgentSession } from '../models/AgentSession';

test('classifies ACP updates by discriminator', () => {
  assert.equal(agentEventRole({ sessionUpdate: 'agent_message_chunk' }), 'agent');
  assert.equal(agentEventRole({ sessionUpdate: 'agent_thought_chunk' }), 'thought');
  assert.equal(agentEventRole({ sessionUpdate: 'tool_call_update' }), 'tool');
  assert.equal(agentEventRole({ sessionUpdate: 'current_mode_update' }), 'system');
});

test('keeps session chrome out of the transcript', () => {
  assert.deepEqual(presentSessionUpdate({ sessionUpdate: 'session_info_update', title: 'Test conversation title' }), {
    kind: 'context', context: { title: 'Test conversation title' },
  });
  assert.deepEqual(presentSessionUpdate({ sessionUpdate: 'current_mode_update', currentModeId: 'ask' }), {
    kind: 'context', context: { mode: 'ask' },
  });
  assert.deepEqual(presentSessionUpdate({ sessionUpdate: 'usage_update', used: 12400, size: 200000 }), {
    kind: 'context', context: { usage: '12k / 200k' },
  });
  assert.equal(presentSessionUpdate({ sessionUpdate: 'available_commands_update', availableCommands: [{ name: 'tests' }] }).kind, 'ignore');
  assert.equal(presentSessionUpdate({ sessionUpdate: 'user_message_chunk', content: { text: 'hi' } }).kind, 'ignore');
});

test('groups agent work between user questions and summarizes sealed runs', () => {
  const items: TranscriptItem[] = [
    { id: '1', role: 'user', text: 'First' },
    { id: '2', role: 'thought', text: 'Looking' },
    { id: '3', role: 'tool', text: 'Search', title: 'Search' },
    { id: '4', role: 'agent', text: 'Done' },
    { id: '5', role: 'user', text: 'Second' },
  ];
  const blocks = groupTranscript(items);
  assert.equal(blocks.length, 4);
  assert.equal(blocks[0]?.type, 'user');
  assert.equal(blocks[1]?.type, 'run');
  assert.equal(blocks[1] && blocks[1].type === 'run' ? blocks[1].items.length : 0, 2);
  assert.equal(blocks[2]?.type, 'reply');
  assert.equal(blocks[3]?.type, 'user');
  assert.equal(runSummary(blocks[1] && blocks[1].type === 'run' ? blocks[1].items : []), 'Worked · 1 tool · Thought');
  assert.equal(liveRunId(blocks), undefined);
  assert.equal(liveRunId(blocks.slice(0, 3)), '2');
});

test('streams thoughts and upserts tool calls', () => {
  let items: TranscriptItem[] = [];
  items = applyPresentedUpdate(items, '1', presentSessionUpdate({ sessionUpdate: 'agent_thought_chunk', content: { text: 'Plan' } }));
  items = applyPresentedUpdate(items, '2', presentSessionUpdate({ sessionUpdate: 'agent_thought_chunk', content: { text: 'ning' } }));
  items = applyPresentedUpdate(items, '3', presentSessionUpdate({ sessionUpdate: 'tool_call', toolCallId: 'call-1', title: 'Read file', status: 'pending', locations: [{ path: '/repo/a.ts' }] }));
  items = applyPresentedUpdate(items, '4', presentSessionUpdate({ sessionUpdate: 'tool_call_update', toolCallId: 'call-1', status: 'completed', content: { text: 'ok' } }));
  assert.equal(items[0]?.text, 'Planning');
  assert.equal(items[1]?.toolCallId, 'call-1');
  assert.equal(items[1]?.status, 'completed');
  assert.equal(items[1]?.title, 'Read file');
  assert.match(items[1]?.text ?? '', /\/repo\/a.ts/);
  assert.match(items[1]?.text ?? '', /ok/);
});

test('replaces the live plan instead of appending rows', () => {
  let items: TranscriptItem[] = [];
  items = applyPresentedUpdate(items, '1', presentSessionUpdate({ sessionUpdate: 'plan', entries: [{ content: 'Inspect', status: 'in_progress' }] }));
  items = applyPresentedUpdate(items, '2', presentSessionUpdate({ sessionUpdate: 'plan', entries: [{ content: 'Inspect', status: 'completed' }, { content: 'Edit', status: 'pending' }] }));
  assert.equal(items.length, 1);
  assert.equal(items[0]?.role, 'plan');
  assert.match(items[0]?.text ?? '', /✓ Inspect/);
});

test('parses both ACP permission shapes', () => {
  const legacy = parsePermission({
    requestId: 'req-1',
    request: {
      toolCall: { title: 'Edit file', kind: 'edit', locations: [{ path: '/repo/a.ts' }] },
      options: [{ optionId: 'allow', name: 'Allow once', kind: 'allow_once' }, { optionId: 'deny', kind: 'reject_once' }],
    },
  });
  assert.equal(legacy?.title, 'Edit file');
  assert.deepEqual(legacy?.locations, ['/repo/a.ts']);
  assert.equal(permissionOptionLabel(legacy!.options[1]!), 'Reject');
  assert.equal(permissionOptionTone('allow_always'), 'allow');

  const current = parsePermission({
    requestId: 'req-2',
    request: {
      title: 'Run the test suite?',
      description: 'cargo test',
      subject: { type: 'command', command: 'cargo test', cwd: '/repo' },
      options: [{ optionId: 'allow-once', name: 'Allow once', kind: 'allow_once' }],
    },
  });
  assert.equal(current?.title, 'Run the test suite?');
  assert.equal(current?.detail, 'cargo test');
  assert.ok(current?.locations.includes('/repo'));
});

test('derives live activity from session state and the latest ACP update', () => {
  assert.equal(resolveActivity({ state: 'ready', hasPermission: false }).label, 'Idle');
  assert.equal(resolveActivity({ state: 'running', hasPermission: true }).label, 'Waiting for a decision');
  assert.equal(resolveActivity({ state: 'running', hasPermission: false, hint: activityHint(presentSessionUpdate({ sessionUpdate: 'agent_thought_chunk', content: { text: '...' } })) }).label, 'Thinking');
  assert.equal(resolveActivity({
    state: 'running',
    hasPermission: false,
    hint: activityHint(presentSessionUpdate({ sessionUpdate: 'tool_call_update', toolCallId: '1', title: 'Read file', status: 'in_progress' })),
  }).label, 'Using Read file');
  assert.equal(resolveActivity({ state: 'authentication_required', hasPermission: false }).label, 'Waiting for sign-in');
  assert.equal(resolveActivity({ state: 'authenticating', hasPermission: false }).label, 'Waiting for sign-in');
  assert.equal(resolveActivity({
    state: 'authentication_required',
    error: '{"code":-32000,"message":"Authentication required"}',
    hasPermission: false,
  }).label, 'Waiting for sign-in');
  assert.equal(resolveActivity({ state: 'ready', error: 'boom', hasPermission: false }).label, 'boom');
});

test('unwraps forwarded session/update envelopes', () => {
  const presented = presentSessionUpdate(unwrapSessionUpdate({ update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'hello' } } }));
  assert.deepEqual(presented, { kind: 'message', role: 'agent', text: 'hello' });
});

test('strips markdown markers from thought display text', () => {
  assert.equal(visibleText('**Planning test suite selection prompt**'), 'Planning test suite selection prompt');
});

test('keeps an authenticating sign-in URL when a later snapshot omits it', () => {
  const withUrl = mergeAgentSession(null, {
    workspaceId: 'ws-1',
    agent: 'codex',
    state: 'authenticating',
    sessionId: 's',
    error: null,
    agents: [],
    loginChallenge: {
      transport: 'redirect',
      url: 'https://idp.example/authorize',
      code: null,
      instructions: 'Open this link and sign in.',
    },
  });
  const stale = mergeAgentSession(withUrl, {
    workspaceId: 'ws-1',
    agent: 'codex',
    state: 'authenticating',
    sessionId: 's',
    error: null,
    agents: [],
  });
  const failed = mergeAgentSession(withUrl, {
    workspaceId: 'ws-1',
    agent: 'codex',
    state: 'authentication_required',
    sessionId: 's',
    error: 'sign-in failed',
    agents: [],
  });

  assert.equal(stale?.loginChallenge?.url, 'https://idp.example/authorize');
  assert.equal(failed?.loginChallenge?.url, undefined);
  assert.equal(failed?.state, 'authentication_required');
});

test('does not let a stale URL-only snapshot erase a device code', () => {
  const complete = mergeAgentSession(null, {
    workspaceId: 'ws-1', agent: 'codex', state: 'authenticating', sessionId: 's', error: null, agents: [],
    loginChallenge: {
      transport: 'code',
      url: 'https://auth.openai.com/codex/device',
      code: 'GGV7-L15JX',
      instructions: 'Open this link, then enter the code shown here.',
      expiresAt: '2026-10-25T10:15:00Z',
    },
  });
  const stale = mergeAgentSession(complete, {
    workspaceId: 'ws-1', agent: 'codex', state: 'authenticating', sessionId: 's', error: null, agents: [],
    loginChallenge: {
      transport: 'code',
      url: 'https://auth.openai.com/codex/device',
      code: null,
      instructions: 'Open this link and enter the code it asks for.',
      canSubmitCode: false,
    },
  });

  assert.equal(stale?.loginChallenge?.code, 'GGV7-L15JX');
  assert.equal(stale?.loginChallenge?.instructions, 'Open this link, then enter the code shown here.');
  assert.equal(stale?.loginChallenge?.expiresAt, '2026-10-25T10:15:00Z');
});

test('keeps the agent catalog when a later snapshot omits it', () => {
  const catalog: AgentSession['agents'] = [
    { agent: 'Codex', available: true, displayName: 'Codex' },
    { agent: 'Cursor', available: false, displayName: 'Cursor' },
  ];
  const loaded = mergeAgentSession(null, {
    workspaceId: 'ws-1', agent: null, state: 'idle', sessionId: null, error: null, agents: catalog,
  });
  const streamedEmpty = mergeAgentSession(loaded, {
    workspaceId: 'ws-1', agent: null, state: 'idle', sessionId: null, error: null, agents: [],
  });
  const stopped = mergeAgentSession(streamedEmpty, null);

  assert.deepEqual(streamedEmpty?.agents, catalog);
  assert.equal(stopped?.agent, null);
  assert.equal(stopped?.state, 'idle');
  assert.deepEqual(stopped?.agents, catalog);
});

test('a permission decision closes only the prompt for the request it decided', () => {
  const open = parsePermission({ requestId: 'req-1', request: { title: 'Write?', options: [{ optionId: 'allow', name: 'Allow', kind: 'allow_once' }] } });

  assert.equal(afterPermissionDecision(open, { requestId: 'other', optionId: 'allow' }), open);
  assert.equal(afterPermissionDecision(open, { requestId: 'req-1', optionId: 'allow' }), null);
  assert.equal(afterPermissionDecision(null, { requestId: 'req-1' }), null);
});

test('userMessageItem marks a message sent mid-turn as steering for the next turn', () => {
  assert.deepEqual(userMessageItem(7, { text: 'focus on tests', steering: true }), { id: '7', role: 'user', text: 'focus on tests', status: steeringStatus });
  assert.equal(userMessageItem(8, { text: 'hello' }).status, undefined);
});

test('consecutive finished reads fold into one row and a turn is timed from its prompt', () => {
  const tool = (id: string, toolKind: string, path: string): TranscriptItem => ({ id, role: 'tool', toolCallId: id, toolKind, status: 'completed', text: path, rawInput: JSON.stringify({ filePath: path }) });
  const entries = groupWork([tool('r1', 'read', 'a.ts'), tool('r2', 'read', 'b.ts'), tool('b', 'execute', 'x'), tool('r3', 'read', 'c.ts')], item => item.text);
  assert.deepEqual(entries.map(entry => entry.type), ['group', 'item', 'item']);
  assert.equal(entries[0]?.type === 'group' && entries[0].title, 'Read 2 files');
  assert.equal(entries[0]?.type === 'group' && entries[0].summary, 'a.ts, b.ts');

  let items = [userMessageItem(1, { text: 'Go' }, '2026-01-01T00:00:00Z')];
  items = applyPresentedUpdate(items, '2', { kind: 'message', role: 'thought', text: 'Hm' }, '2026-01-01T00:00:10Z');
  items = applyPresentedUpdate(items, '3', { kind: 'message', role: 'thought', text: 'm' }, '2026-01-01T00:00:14Z');
  items = applyPresentedUpdate(items, '4', { kind: 'message', role: 'agent', text: 'Done' }, '2026-01-01T00:00:20Z');
  const run = groupTranscript(items).find(block => block.type === 'run');
  assert.equal(run?.type === 'run' && run.duration, '20s');
  assert.equal(thoughtLabel(items[1]!, false), 'Thought for 4s');
  assert.equal(thoughtLabel(items[1]!, true), 'Thinking');
});

test('a steering turn that follows with no prompt gets its own reply instead of joining the last one', () => {
  let items = [userMessageItem(1, { text: 'Edit it' }, '2026-01-01T00:00:00Z'), userMessageItem(2, { text: 'Then show code', steering: true }, '2026-01-01T00:00:01Z')];
  items = applyPresentedUpdate(items, '3', { kind: 'message', role: 'agent', text: 'Edited.' }, '2026-01-01T00:00:10Z');
  items = endTurn(items, '2026-01-01T00:00:11Z');
  items = applyPresentedUpdate(items, '5', { kind: 'message', role: 'agent', text: '```ts\nconst port = 4000;\n```' }, '2026-01-01T00:00:20Z');
  const replies = groupTranscript(items).flatMap(block => block.type === 'reply' ? [block.item.text] : []);
  assert.deepEqual(replies, ['Edited.', '```ts\nconst port = 4000;\n```']);
  assert.deepEqual(endTurn([], 'x'), []);
});

test('a collapsed run names failed tools, and no run stays open once the session stops', () => {
  const items: TranscriptItem[] = [
    { id: 't1', role: 'tool', status: 'failed', text: 'npm run lint' },
    { id: 't2', role: 'tool', status: 'completed', text: 'edit' },
  ];
  assert.equal(runSummary(items, '9s'), 'Worked for 9s · 2 tools · 1 failed');
  const blocks = groupTranscript([userMessageItem(1, { text: 'Go' }), ...items]);
  assert.equal(liveRunId(blocks), 't1');
  assert.equal(liveRunId(blocks, false), undefined);
});

test('the transcript follows new output only while the reader is at the end', () => {
  assert.equal(isFollowing(920, 800, 1760), true);
  assert.equal(isFollowing(500, 800, 1760), false);
});
