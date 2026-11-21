import assert from 'node:assert/strict';
import test from 'node:test';
import { boundAgentActionLabel, formatLoopLine, loopSettingsSummary } from './LoopLineProvider';

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

test('loopSettingsSummary counts the open checks', () => {
  const autonomy = {
    enabled: false,
    preconditions: [
      { id: 'forgeCapabilityEnabled', satisfied: false, remedy: 'Enable a forge' },
      { id: 'agentAvailable', satisfied: true, remedy: null },
    ],
  };
  assert.equal(loopSettingsSummary(autonomy), 'Unattended loop · 1 of 2 checks open');
});

test('loopSettingsSummary is ready when every check passes and null without a chain', () => {
  const ready = { enabled: true, preconditions: [{ id: 'autonomyEnabled', satisfied: true, remedy: null }] };
  assert.equal(loopSettingsSummary(ready), 'Unattended loop ready');
  assert.equal(loopSettingsSummary(null), null);
  assert.equal(loopSettingsSummary({ enabled: false, preconditions: [] }), null);
});
