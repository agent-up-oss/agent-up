import assert from 'node:assert/strict';
import test from 'node:test';
import type { ServerAutonomy } from '../models/AgentSession';
import { canActivateLoop, canCancelLoop, canEnableAutonomy, preconditionLabel } from './LoopSurfaceProvider';

const readyToEnable: ServerAutonomy = {
  enabled: false,
  preconditions: [
    { id: 'forgeCapabilityEnabled', satisfied: true },
    { id: 'hostConnectionAuthorized', satisfied: true },
    { id: 'agentAvailable', satisfied: true },
    { id: 'autonomyEnabled', satisfied: false, remedy: 'Enable autonomy for this Server' },
  ],
};

const readyToActivate: ServerAutonomy = {
  enabled: true,
  preconditions: readyToEnable.preconditions.map(item =>
    item.id === 'autonomyEnabled' ? { ...item, satisfied: true, remedy: null } : item),
};

test('preconditionLabel uses the Server chain names', () => {
  assert.equal(preconditionLabel('forgeCapabilityEnabled'), 'Forge capability');
  assert.equal(preconditionLabel('hostConnectionAuthorized'), 'Host connection');
  assert.equal(preconditionLabel('agentAvailable'), 'Agent available');
  assert.equal(preconditionLabel('autonomyEnabled'), 'Autonomy enabled');
});

test('canEnableAutonomy requires the first three preconditions', () => {
  assert.equal(canEnableAutonomy(readyToEnable), true);
  assert.equal(canEnableAutonomy(readyToActivate), false);
  assert.equal(canEnableAutonomy({
    ...readyToEnable,
    preconditions: readyToEnable.preconditions.map(item =>
      item.id === 'hostConnectionAuthorized' ? { ...item, satisfied: false } : item),
  }), false);
});

test('canActivateLoop requires the full chain and a stopped loop', () => {
  assert.equal(canActivateLoop(readyToActivate, { active: false, phase: 'idle' }), true);
  assert.equal(canActivateLoop(readyToActivate, { active: true, phase: 'running' }), false);
  assert.equal(canActivateLoop(readyToEnable, { active: false, phase: 'idle' }), false);
});

test('canCancelLoop follows the typed active flag', () => {
  assert.equal(canCancelLoop({ active: true, phase: 'running' }), true);
  assert.equal(canCancelLoop({ active: false, phase: 'stopped', terminal: 'Cancelled' }), false);
});
