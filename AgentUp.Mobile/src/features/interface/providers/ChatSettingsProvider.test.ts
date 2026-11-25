import assert from 'node:assert/strict';
import test from 'node:test';
import { chatSettingsFields, settingsWorkspaces } from './ChatSettingsProvider';

test('the autonomous toggle and agents are the only member settings fields', () => {
  const rows = chatSettingsFields({
    autonomy: { enabled: false, preconditions: [{ id: 'permissionPolicy', satisfied: false, remedy: 'Add a policy.' }] },
    loop: null,
    agents: [
      { agent: 'codex', available: true, displayName: 'Codex' },
      { agent: 'claude', available: true, displayName: 'Claude' },
    ],
    boundAgent: 'claude',
  });

  assert.deepEqual(rows.map(row => row.id), ['autonomy', 'codex', 'claude']);
  assert.equal(rows[0]?.action, 'toggle-autonomy');
  assert.equal(rows[0]?.text?.detail, 'Off');
  assert.equal(rows.find(row => row.id === 'claude')?.text?.action, 'Bound');
  assert.equal(rows.find(row => row.id === 'codex')?.action, 'select-agent');
  assert.equal(rows.some(row => row.id === 'permissionPolicy'), false);
});

test('an active loop turns the autonomous toggle off with Disable', () => {
  const [autonomy] = chatSettingsFields({
    autonomy: { enabled: true, preconditions: [] },
    loop: { active: true, phase: 'working', taskNumber: 44, taskTitle: 'Restock' },
    agents: [],
    boundAgent: null,
  });

  assert.equal(autonomy.action, 'cancel-loop');
  assert.equal(autonomy.text?.detail, 'On');
  assert.equal(autonomy.text?.action, 'Disable');
});

test('the selected workspace tab is marked', () => {
  const tabs = settingsWorkspaces([{ workspaceId: 'bob', displayName: 'Bob' }, { workspaceId: 'mia', displayName: 'Mia' }], 'mia');

  assert.deepEqual(tabs.map(tab => tab.state), [undefined, 'au-app-tab--selected']);
  assert.deepEqual(tabs.map(tab => tab.action), ['settings-workspace', 'settings-workspace']);
});
