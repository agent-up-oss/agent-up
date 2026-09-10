import assert from 'node:assert/strict';
import test from 'node:test';
import { agentEventRole, agentEventText } from './AgentEventPresentationProvider';

test('renders text chunks, tool status, and nested plan entries', () => {
  assert.equal(agentEventText({ content: { type: 'text', text: 'hello' } }), 'hello');
  assert.equal(agentEventText({ title: 'Run tests', status: 'completed' }), 'Run tests · completed');
  assert.equal(agentEventText({ entries: [{ content: { text: 'first' } }, { text: 'second' }] }), 'first\nsecond');
});

test('classifies ACP updates by discriminator', () => {
  assert.equal(agentEventRole({ sessionUpdate: 'agent_message_chunk' }), 'agent');
  assert.equal(agentEventRole({ sessionUpdate: 'agent_thought_chunk' }), 'thought');
  assert.equal(agentEventRole({ sessionUpdate: 'tool_call_update' }), 'tool');
  assert.equal(agentEventRole({ sessionUpdate: 'current_mode_update' }), 'system');
});
