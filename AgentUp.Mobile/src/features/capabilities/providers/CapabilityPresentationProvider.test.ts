import assert from 'node:assert/strict';
import { test } from 'node:test';
import type { CapabilityModule, CapabilitySetting } from '../models/CapabilityModule';
import { capabilityButton, capabilityStatus, changedSettings, settingLabel, settingPlaceholder, showsSettings } from './CapabilityPresentationProvider';

function capability(patch: Partial<CapabilityModule> = {}): CapabilityModule {
  return {
    id: 'opencode', version: '1.0.0', displayName: 'OpenCode', publisher: 'agent-up', kind: 'agent',
    enabled: false, available: true, state: 'disabled', canRun: false, missing: [], settings: [], messages: [],
    ...patch,
  };
}

const endpoint: CapabilitySetting = { key: 'endpoint', label: 'Endpoint', kind: 'url', required: true, placeholder: 'http://localhost:11434', value: 'http://old.test', hasValue: true };
const token: CapabilitySetting = { key: 'token', label: 'Token', kind: 'secret', required: false, value: null, hasValue: true };

test('a capability whose software was not found says so in its disabled button', () => {
  assert.deepEqual(capabilityButton(capability({ available: false, state: 'unavailable', missing: ['nix'] })), { label: 'Needs Nix', disabled: true });
  assert.deepEqual(capabilityButton(capability({ available: false, state: 'unavailable', missing: ['agent'] })), { label: 'Not found', disabled: true });
  assert.deepEqual(capabilityButton(capability()), { label: 'Enable', disabled: false });
  assert.deepEqual(capabilityButton(capability({ enabled: true })), { label: 'Disable', disabled: false });
});

test('a Server without availability is treated as having found the software', () => {
  assert.deepEqual(capabilityButton(capability({ available: undefined })), { label: 'Enable', disabled: false });
});

test('the status is one short word for each Server state', () => {
  assert.deepEqual(
    ['ready', 'needsSetup', 'unavailable', 'disabled'].map(state => capabilityStatus(capability({ state }))),
    ['Ready', 'Needs setup', 'Not found', 'Off'],
  );
  assert.equal(capabilityStatus(capability({ state: 'unavailable', missing: ['codex-acp'] })), 'Missing codex-acp');
});

test('settings expand only once the capability is enabled', () => {
  assert.equal(showsSettings(capability({ settings: [endpoint] })), false);
  assert.equal(showsSettings(capability({ enabled: true, settings: [endpoint] })), true);
  assert.equal(showsSettings(capability({ enabled: true })), false);
});

test('only edited settings are sent, and an untouched secret is kept', () => {
  assert.deepEqual(changedSettings([endpoint, token], { endpoint: ' http://new.test ', token: '' }), { endpoint: 'http://new.test' });
  assert.deepEqual(changedSettings([endpoint, token], { endpoint: 'http://old.test', token: 'sk-new' }), { token: 'sk-new' });
});

test('a stored secret reads as stored and an optional field says so', () => {
  assert.equal(settingPlaceholder(token), 'Stored');
  assert.equal(settingPlaceholder(endpoint), 'http://localhost:11434');
  assert.equal(settingLabel(token), 'Token (optional)');
});
