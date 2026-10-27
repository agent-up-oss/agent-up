import assert from 'node:assert/strict';
import test from 'node:test';
import type { Workspace } from '@/features/workspaces/models/Workspace';
import {
  agentSubtitle,
  applicationAddress,
  checkSummary,
  findingSummary,
  taskFacts,
} from './AgentWorkspace';
import type { TeamStanding } from './TeamStanding';

function standing(extra: Partial<TeamStanding> = {}): TeamStanding {
  return {
    workspaceId: 'lucy',
    displayName: 'Lucy',
    branch: 'feat/44-restock',
    state: 'Running',
    agent: 'Claude',
    kind: 'waiting',
    taskNumber: 44,
    taskTitle: 'Restock featured products',
    reason: null,
    diagnosticCode: null,
    permissionRuleIds: [],
    forge: {
      availability: 'ready',
      proposalNumber: '431',
      proposalTitle: 'Restock the featured rail',
      proposalUrl: 'u',
      checksPassed: 3,
      checksFailed: 0,
      checksRunning: 0,
      checksSkipped: 0,
      checksTotal: 3,
      unresolvedThreads: 2,
      mergeState: 'clean',
      detail: null,
      checks: 'passed',
      verdicts: [],
    },
    role: 'reviewer',
    roleTools: [],
    identity: { login: 'lucy-aup[bot]', displayName: 'Lucy', isBot: true, url: 'u' },
    reviews: 'pm',
    slots: [],
    ...extra,
  };
}

const workspace = { branch: 'feat/44-restock', commit: 'a71f093' } as Workspace;

test('the task facts answer what the work is, what was proposed, and where it was built', () => {
  assert.deepEqual(taskFacts(standing(), workspace), [
    { label: 'Task', value: '#44 · Restock featured products' },
    { label: 'Proposal', value: '#431 · lucy-aup[bot]' },
    { label: 'Branch', value: 'feat/44-restock' },
    { label: 'Head', value: 'a71f093' },
  ]);
});

test('a fact the Server did not answer is left out rather than shown blank', () => {
  // An empty row reads as an answer of nothing, which is not the same as a question nobody asked.
  const bare = standing({ taskNumber: null, taskTitle: null, forge: { ...standing().forge, proposalNumber: null } });

  assert.deepEqual(taskFacts(bare, null).map(fact => fact.label), ['Branch']);
});

test('the workspace branch wins over the one the standing carried', () => {
  const facts = taskFacts(standing({ branch: 'stale' }), { branch: 'feat/live', commit: 'b2' } as Workspace);

  assert.equal(facts.find(fact => fact.label === 'Branch')?.value, 'feat/live');
});

test('an application says the port it was given and what speaks there', () => {
  assert.equal(
    applicationAddress({ name: 'Storefront', state: 'Running', allocatedPorts: [{ allocatedPort: 9100, protocol: 'http' }] }),
    ':9100 · http',
  );
});

test('an application with no port says so rather than showing a blank', () => {
  assert.equal(applicationAddress({ name: 'Sample', state: 'Running' }), 'no port');
  assert.equal(applicationAddress({ name: 'Sample', state: 'Running', kind: 'Desktop' }), 'desktop · no port');
});

test('the findings say whether anything is waiting on a reader', () => {
  assert.equal(findingSummary(standing()), '2 unresolved threads');
  assert.equal(findingSummary(standing({ forge: { ...standing().forge, unresolvedThreads: 1 } })), '1 unresolved thread');
  assert.equal(findingSummary(standing({ forge: { ...standing().forge, unresolvedThreads: 0 } })), 'No unresolved threads.');
});

test('findings and checks are only worth counting once a proposal is open', () => {
  const none = standing({ forge: { ...standing().forge, proposalNumber: null } });

  assert.equal(findingSummary(none), 'No proposal open.');
  assert.equal(checkSummary(none), 'No checks reported.');
});

test('the checks read as counts, naming only the states that happened', () => {
  assert.equal(checkSummary(standing()), '3 passed');
  assert.equal(
    checkSummary(standing({ forge: { ...standing().forge, checksFailed: 1, checksRunning: 2, checksTotal: 6 } })),
    '3 passed · 1 failed · 2 running',
  );
});

test('an agent reads as its role and the account it acts as', () => {
  assert.equal(agentSubtitle(standing()), 'Reviewer · @lucy-aup[bot] · Claude');
});

test('an agent with no role or account still says what it is doing', () => {
  assert.equal(agentSubtitle(standing({ role: 'none', identity: null, agent: null })), 'Running');
});
