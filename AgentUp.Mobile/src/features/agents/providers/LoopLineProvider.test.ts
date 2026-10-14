import assert from 'node:assert/strict';
import test from 'node:test';
import { boundAgentActionLabel, formatLoopLine } from './LoopLineProvider';

test('formatLoopLine joins phase, task and stop reason', () => {
  const line = formatLoopLine({
    active: true,
    phase: 'blocked',
    taskTitle: 'Ship the review',
    terminal: 'BlockedPolicy',
    stopReason: 'policy',
  });
  assert.equal(line, 'blocked · Ship the review · BlockedPolicy · policy');
});

test('formatLoopLine is null when the session has no loop', () => {
  assert.equal(formatLoopLine(null), null);
});

test('boundAgentActionLabel uses displayName and ignores agent id case', () => {
  const agents = [{ agent: 'codex', available: true, displayName: 'Codex' }];
  assert.equal(boundAgentActionLabel('Codex', 'codex', agents), 'Open Codex');
  assert.equal(boundAgentActionLabel('Codex', 'cursor', agents), 'Start Codex');
});
