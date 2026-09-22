import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  statusDotStyle,
  statusLedRole,
  workspaceLedState,
  workspaceLifecycleControls,
  workspaceShowsApplications,
  workspaceStatusLabel,
} from './WorkspaceStatusProvider';

test('status LEDs follow health and keep Starting yellow before probes publish', () => {
  assert.equal(statusLedRole('Healthy'), 'healthy');
  assert.equal(statusLedRole('Running'), 'healthy');
  assert.equal(statusLedRole('Checking'), 'warning');
  assert.equal(statusLedRole('Starting'), 'warning');
  assert.equal(statusLedRole('Stopping'), 'warning');
  assert.equal(statusLedRole('Unhealthy'), 'danger');
  assert.equal(statusLedRole('Failed'), 'danger');
  assert.equal(statusLedRole('Stopped'), 'idle');
});

test('workspace LED prefers Server healthState when the stream supplies it', () => {
  assert.equal(statusLedRole(workspaceLedState('Starting')), 'warning');
  assert.equal(statusLedRole(workspaceLedState('Running', 'Checking')), 'warning');
  assert.equal(statusLedRole(workspaceLedState('Running', 'Healthy')), 'healthy');
  assert.equal(statusLedRole(workspaceLedState('Running', 'Unhealthy')), 'danger');
  assert.equal(statusLedRole(workspaceLedState('Failed')), 'danger');
  assert.equal(statusLedRole(workspaceLedState('Stopped', 'Healthy')), 'idle');
});

test('status labels keep lifecycle text until the workspace is running', () => {
  assert.equal(workspaceStatusLabel('Starting', 'Checking'), 'Starting');
  assert.equal(workspaceStatusLabel('Running', 'Checking'), 'Checking');
  assert.equal(workspaceStatusLabel('Running', 'Healthy'), 'Healthy');
  assert.equal(workspaceStatusLabel('Running'), 'Running');
  assert.equal(workspaceStatusLabel('Failed', 'Unhealthy'), 'Failed');
});

test('stopped workspaces do not present application rows', () => {
  assert.equal(workspaceShowsApplications('Running'), true);
  assert.equal(workspaceShowsApplications('Starting'), true);
  assert.equal(workspaceShowsApplications('Stopped'), false);
  assert.equal(workspaceShowsApplications('Failed'), false);
});

test('lifecycle controls match Desktop start/stop visibility', () => {
  assert.deepEqual(workspaceLifecycleControls('Stopped'), {
    action: 'start', glyph: '▶', accessibilityLabel: 'Start workspace', busy: false,
  });
  assert.deepEqual(workspaceLifecycleControls('Failed'), {
    action: 'start', glyph: '▶', accessibilityLabel: 'Start workspace', busy: false,
  });
  assert.deepEqual(workspaceLifecycleControls('Stopping'), {
    action: 'start', glyph: '▶', accessibilityLabel: 'Start workspace', busy: true,
  });
  assert.deepEqual(workspaceLifecycleControls('Running'), {
    action: 'stop', glyph: '■', accessibilityLabel: 'Stop workspace', busy: false,
  });
  assert.deepEqual(workspaceLifecycleControls('Starting'), {
    action: 'stop', glyph: '■', accessibilityLabel: 'Stop workspace', busy: true,
  });
});

test('status dots use the design-system healthy warning and danger tokens', () => {
  assert.equal(statusDotStyle('Running').backgroundColor, '#22c55e');
  assert.equal(statusDotStyle('Starting').backgroundColor, '#e0a128');
  assert.equal(statusDotStyle('Failed').backgroundColor, '#d84f4f');
  assert.equal(statusDotStyle('Stopped').backgroundColor, '#8a9a92');
});
