import assert from 'node:assert/strict';
import test from 'node:test';
import {
  activityHint,
  agentEventRole,
  applyPresentedUpdate,
  parsePermission,
  permissionOptionLabel,
  permissionOptionTone,
  presentSessionUpdate,
  resolveActivity,
  unwrapSessionUpdate,
  visibleText,
} from './AgentEventPresentationProvider';
import type { TranscriptItem } from '../models/AgentSession';

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
});

test('unwraps forwarded session/update envelopes', () => {
  const presented = presentSessionUpdate(unwrapSessionUpdate({ update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'hello' } } }));
  assert.deepEqual(presented, { kind: 'message', role: 'agent', text: 'hello' });
});

test('strips markdown markers from thought display text', () => {
  assert.equal(visibleText('**Planning test suite selection prompt**'), 'Planning test suite selection prompt');
});
