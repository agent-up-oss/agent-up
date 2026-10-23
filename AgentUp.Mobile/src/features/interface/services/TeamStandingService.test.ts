import assert from 'node:assert/strict';
import test from 'node:test';
import { noForge, type TeamStanding } from '../models/TeamStanding';
import { checksSummary, forgeStatus, headlineFor, identityHandle, roleLabel, showProposalBesideTask, stateLabel, taskLabel, threadSummary, toolsSummary, triage, whoIs } from './TeamStandingService';

function standing(partial: Partial<TeamStanding> = {}): TeamStanding {
  return {
    workspaceId: 'shop',
    displayName: 'Bob',
    branch: 'main',
    state: 'Running',
    agent: 'Claude',
    kind: 'working',
    taskNumber: null,
    taskTitle: null,
    reason: null,
    diagnosticCode: null,
    permissionRuleIds: [],
    forge: noForge,
    role: 'none',
    roleTools: [],
    identity: null,
    ...partial,
  };
}

test('triage splits the team into the groups the bridge draws', () => {
  const result = triage([
    standing({ workspaceId: 'shop', kind: 'working', taskNumber: 44 }),
    standing({ workspaceId: 'checkout', kind: 'waiting', taskNumber: 48 }),
    standing({ workspaceId: 'docs', kind: 'halted', taskNumber: 51 }),
    standing({ workspaceId: 'fresh', kind: 'idle' }),
  ]);
  assert.equal(result.team.length, 4);
  assert.deepEqual(result.waiting.map(item => item.workspaceId), ['checkout']);
  assert.deepEqual(result.halted.map(item => item.workspaceId), ['docs']);
  assert.equal(result.tasks.length, 3);
  assert.equal(result.headline, '4 agents · 1 waiting on you · 1 halted');
});

test('triage of an empty team reads as nothing rather than as unread', () => {
  const result = triage([]);
  assert.equal(result.headline, '0 agents · 0 waiting on you · 0 halted');
  assert.deepEqual(result.tasks, []);
});

test('triage says one agent in the singular', () => {
  assert.equal(triage([standing()]).headline, '1 agent · 0 waiting on you · 0 halted');
});

test('state labels and headlines name what each kind needs', () => {
  assert.equal(stateLabel('waiting'), '◆ gate open');
  assert.equal(stateLabel('halted'), '▲ halted');
  assert.equal(stateLabel('working'), '● working');
  assert.equal(stateLabel('idle'), '○ idle');
  assert.equal(headlineFor(standing({ kind: 'waiting', taskNumber: 48 })), 'Bob is waiting on you on #48');
  assert.equal(headlineFor(standing({ kind: 'halted', taskNumber: 51 })), 'Bob stopped on #51');
  assert.equal(headlineFor(standing({ kind: 'idle' })), 'Bob has nothing in hand');
});

test('a task label pairs the number with the title when there is one', () => {
  assert.equal(taskLabel(standing({ taskNumber: 44, taskTitle: 'Restock' })), '#44 · Restock');
  assert.equal(taskLabel(standing({ taskNumber: 44 })), '#44');
  assert.equal(taskLabel(standing()), null);
});

test('summaries say what was not reported instead of showing zeroes', () => {
  assert.equal(checksSummary(standing()), 'no checks reported');
  assert.equal(forgeStatus(standing()), 'no host connected');
  assert.equal(
    checksSummary(standing({ forge: { ...noForge, checksPassed: 1, checksFailed: 1, checksRunning: 1 } })),
    '1 passing · 1 failing · 1 running',
  );
  assert.equal(forgeStatus(standing({ forge: { ...noForge, availability: 'ready', mergeState: 'clean' } })), 'mergeable: clean');
  assert.equal(forgeStatus(standing({ forge: { ...noForge, availability: 'unreachable' } })), 'host unreachable');
  assert.equal(forgeStatus(standing({ forge: { ...noForge, availability: 'needsCredential' } })), 'host needs a credential');
  assert.equal(threadSummary(standing({ forge: { ...noForge, unresolvedThreads: 1 } })), '1 unresolved review thread');
  assert.equal(threadSummary(standing({ forge: { ...noForge, unresolvedThreads: 2 } })), '2 unresolved review threads');
});

test('a proposal is named beside the task only when it is a different number', () => {
  const forge = { ...noForge, availability: 'ready', proposalNumber: '52', proposalTitle: 'Restock, split out' };
  assert.equal(showProposalBesideTask(standing({ taskNumber: 44, forge })), true);
  assert.equal(showProposalBesideTask(standing({ taskNumber: 44, forge: { ...forge, proposalNumber: '44' } })), false);
  assert.equal(showProposalBesideTask(standing({ taskNumber: 44 })), false);
});

test('a role reads as one word, and as nothing when none is configured', () => {
  assert.equal(roleLabel(standing({ role: 'developer' })), 'Developer');
  assert.equal(roleLabel(standing({ role: 'reviewer' })), 'Reviewer');
  assert.equal(roleLabel(standing({ role: 'other' })), 'Custom role');
  assert.equal(roleLabel(standing()), null);
});

test('the identity reads as the handle a reviewer sees on a thread', () => {
  const identity = { login: 'lucy-aup[bot]', displayName: 'Lucy', isBot: true, url: 'u' };
  assert.equal(identityHandle(standing({ identity })), '@lucy-aup[bot]');
  assert.equal(identityHandle(standing({ identity: { ...identity, login: '@lucy' } })), '@lucy');
  assert.equal(identityHandle(standing()), null);
});

test('who an agent is drops the parts the Server did not report', () => {
  const identity = { login: 'lucy-aup[bot]', displayName: 'Lucy', isBot: true, url: 'u' };
  assert.equal(
    whoIs(standing({ role: 'reviewer', identity, agent: 'Claude', state: 'Running' })),
    'Reviewer · @lucy-aup[bot] · Claude · Running',
  );
  assert.equal(whoIs(standing({ agent: null, state: 'Stopped' })), 'Stopped');
});

test('the tools summary is everything the role may do, or nothing at all', () => {
  assert.equal(toolsSummary(standing({ roleTools: ['play_validation_flow', 'browser_screenshot'] })),
    'may play_validation_flow, browser_screenshot');
  assert.equal(toolsSummary(standing()), null);
});
