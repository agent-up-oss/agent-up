import assert from 'node:assert/strict';
import test from 'node:test';
import { chatSettingsFields, settingsWorkspaces } from './ChatSettingsProvider';

const ready = (id: string) => ({ id, satisfied: true });

test('autonomy can be enabled once the forge and host are ready, and the loop sits under it', () => {
  const rows = chatSettingsFields({
    autonomy: { enabled: false, preconditions: [ready('forgeCapabilityEnabled'), ready('hostConnectionAuthorized'), { id: 'autonomyEnabled', satisfied: false, remedy: 'Enable autonomy for this Server' }] },
    loop: null,
    agents: [],
    boundAgent: null,
  });
  const autonomy = rows.find(row => row.id === 'autonomyEnabled');

  assert.deepEqual(rows.map(row => row.text?.detail), ['Ready', 'Ready', 'Enable autonomy for this Server']);
  assert.equal(autonomy?.action, 'enable-autonomy');
  assert.equal(autonomy?.items?.fields?.[0]?.id, 'loop');
  assert.equal(autonomy?.items?.fields?.[0]?.action, undefined);
});

test('a field the client cannot change is locked and names the remedy', () => {
  const [policy] = chatSettingsFields({
    autonomy: { enabled: true, preconditions: [{ id: 'permissionPolicy', satisfied: false, remedy: 'Add a permission policy before activating the loop.' }] },
    loop: null,
    agents: [],
    boundAgent: null,
  });

  assert.deepEqual(policy.parts, ['control', 'locked']);
  assert.equal(policy.action, undefined);
  assert.equal(policy.text?.detail, 'Add a permission policy before activating the loop.');
});

test('agents can be used, the bound one is marked, and an active loop can be stopped', () => {
  const rows = chatSettingsFields({
    autonomy: { enabled: true, preconditions: [] },
    loop: { active: true, phase: 'working', taskNumber: 44, taskTitle: 'Restock' },
    agents: [
      { agent: 'codex', available: true, displayName: 'Codex' },
      { agent: 'claude', available: true, displayName: 'Claude' },
    ],
    boundAgent: 'claude',
  });
  const agents = rows.find(row => row.id === 'agents')?.items?.fields ?? [];
  const loop = rows.find(row => row.id === 'loop');

  assert.deepEqual(agents.map(row => [row.text?.action, row.action]), [['Use', 'select-agent'], ['Bound', undefined]]);
  assert.equal(loop?.text?.detail, 'Working #44 · Restock');
  assert.equal(loop?.items?.fields?.[0]?.action, 'cancel-loop');
});

test('the selected workspace tab is marked', () => {
  const tabs = settingsWorkspaces([{ workspaceId: 'bob', displayName: 'Bob' }, { workspaceId: 'mia', displayName: 'Mia' }], 'mia');

  assert.deepEqual(tabs.map(tab => tab.state), [undefined, 'au-app-tab--selected']);
  assert.deepEqual(tabs.map(tab => tab.action), ['settings-workspace', 'settings-workspace']);
});
