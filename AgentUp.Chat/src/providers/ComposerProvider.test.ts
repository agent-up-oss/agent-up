import assert from 'node:assert/strict';
import test from 'node:test';
import { composerState } from './ComposerProvider';

test('a running turn keeps the field editable and offers Steer', () => {
  assert.deepEqual(composerState({ state: 'running', permission: false, busy: false }), {
    steering: true,
    blocked: false,
    editable: true,
    action: 'Steer',
  });
});

test('an idle session offers Send in an editable field', () => {
  assert.deepEqual(composerState({ state: 'ready', permission: false, busy: false }), {
    steering: false,
    blocked: false,
    editable: true,
    action: 'Send',
  });
});

test('a permission request blocks the composer even while the turn runs', () => {
  const state = composerState({ state: 'running', permission: true, busy: false });
  assert.equal(state.blocked, true);
  assert.equal(state.editable, false);
  assert.equal(state.action, 'Send');
});

test('a sign-in in progress or a send in flight blocks the composer', () => {
  for (const input of [
    { state: 'authentication_required', permission: false, busy: false },
    { state: 'authenticating', permission: false, busy: false },
    { state: 'ready', permission: false, busy: true },
  ]) {
    const state = composerState(input);
    assert.equal(state.blocked, true, input.state);
    assert.equal(state.editable, false, input.state);
  }
});

test('the field is editable exactly when the button can send', () => {
  for (const state of ['running', 'ready', 'authenticating', null])
    for (const permission of [true, false])
      for (const busy of [true, false]) {
        const composer = composerState({ state, permission, busy });
        assert.equal(composer.editable, !composer.blocked, `${state} ${permission} ${busy}`);
      }
});
