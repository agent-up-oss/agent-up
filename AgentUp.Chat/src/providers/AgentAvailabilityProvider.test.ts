import assert from 'node:assert/strict';
import test from 'node:test';
import { agentRowDetail, presentAgentRow } from './AgentAvailabilityProvider';

test('presentAgentRow marks an available agent as selectable', () => {
  const row = presentAgentRow({ agent: 'codex', available: true, displayName: 'Codex' });
  assert.equal(row.canSelect, true);
  assert.equal(row.canEnable, false);
  assert.equal(row.status, 'Available');
  assert.equal(agentRowDetail({ agent: 'codex', available: true, displayName: 'Codex' }), 'Create a new session');
});

test('presentAgentRow offers enable when the package is the gap', () => {
  const agent = {
    agent: 'claude',
    available: false,
    displayName: 'Claude',
    unavailability: 'capabilityNotEnabled' as const,
    detail: 'Claude is not enabled on this Server.',
    remedy: 'Enable the claude capability.',
  };
  const row = presentAgentRow(agent);
  assert.equal(row.canEnable, true);
  assert.equal(row.status, 'Not enabled');
  assert.equal(agentRowDetail(agent), 'Claude is not enabled on this Server. Enable the claude capability.');
});

test('presentAgentRow blocks a missing executable without an enable action', () => {
  const row = presentAgentRow({
    agent: 'cursor',
    available: false,
    displayName: 'Cursor',
    unavailability: 'executableNotFound',
    detail: 'cursor ACP executable is not installed or is not on PATH.',
    remedy: 'Install agent and keep it on PATH.',
  });
  assert.equal(row.canSelect, false);
  assert.equal(row.canEnable, false);
  assert.equal(row.isBlocked, true);
  assert.equal(row.status, 'Executable not found');
});
