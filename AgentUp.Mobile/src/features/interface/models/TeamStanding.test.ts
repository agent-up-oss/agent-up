import assert from 'node:assert/strict';
import test from 'node:test';
import type { ForgeHosting } from '@/features/git/models/ForgeHosting';
import type { AgentSession } from '@agent-up/chat';
import { classifyRole, composeStanding, countForge, noForge, type TeamMember } from './TeamStanding';

const member: TeamMember = { workspaceId: 'shop', displayName: 'Bob', branch: 'main', state: 'Running' };

function session(partial: Partial<AgentSession> = {}): AgentSession {
  return { workspaceId: 'shop', agent: 'Claude', state: 'ready', sessionId: null, error: null, agents: [], ...partial };
}

function hosting(partial: Partial<ForgeHosting> = {}): ForgeHosting {
  return {
    availability: 'ready',
    moduleId: 'github',
    proposal: null,
    checks: [],
    threads: [],
    mergeability: null,
    detail: null,
    ...partial,
  };
}

test('a running loop composes as working and carries its task', () => {
  const standing = composeStanding(
    member,
    session({ loop: { active: true, phase: 'running', taskNumber: 44, taskTitle: 'Restock', terminal: 'Running' } }),
    null,
  );
  assert.equal(standing.kind, 'working');
  assert.equal(standing.taskNumber, 44);
  assert.equal(standing.agent, 'Claude');
  assert.deepEqual(standing.forge, noForge);
});

test('a blocked loop composes as waiting and keeps the permissions it needs', () => {
  const standing = composeStanding(
    member,
    session({
      state: 'awaiting_input',
      loop: {
        active: true,
        phase: 'blocked',
        taskNumber: 48,
        terminal: 'BlockedAwaitingInput',
        blockReason: 'Waiting for you to approve the review before it is posted.',
        permissionRuleIds: ['forge.submit_review'],
      },
    }),
    null,
  );
  assert.equal(standing.kind, 'waiting');
  assert.match(standing.reason ?? '', /approve the review/);
  assert.deepEqual(standing.permissionRuleIds, ['forge.submit_review']);
});

test('a failed loop composes as halted and prefers the host diagnostic as the reason', () => {
  const standing = composeStanding(
    member,
    session({ state: 'error', loop: { active: false, phase: 'failed', terminal: 'Failed', stopReason: 'forge.unreachable' } }),
    hosting({ availability: 'unreachable', detail: 'The repository host returned 502 on read.' }),
  );
  assert.equal(standing.kind, 'halted');
  assert.equal(standing.diagnosticCode, 'forge.unreachable');
  assert.match(standing.reason ?? '', /502/);
});

test('an empty queue composes as idle rather than halted', () => {
  const standing = composeStanding(
    member,
    session({ loop: { active: false, phase: 'idle', stopReason: 'queue_empty' } }),
    null,
  );
  assert.equal(standing.kind, 'idle');
  assert.equal(standing.taskNumber, null);
});

test('a workspace with neither read still stands', () => {
  const standing = composeStanding(member, null, null);
  assert.equal(standing.kind, 'idle');
  assert.equal(standing.agent, null);
  assert.equal(standing.forge.availability, 'none');
});

test('countForge counts checks by conclusion and threads by resolution', () => {
  const counted = countForge(hosting({
    proposal: { id: 'PR', number: '48', title: 'Express checkout', url: 'https://example/48', headRef: 'h', baseRef: 'main', state: 'open' },
    checks: [
      { name: 'build', status: 'completed', conclusion: 'success', url: '' },
      { name: 'e2e', status: 'completed', conclusion: 'failure', url: '' },
      { name: 'lint', status: 'in_progress', conclusion: '', url: '' },
    ],
    threads: [
      { id: 't1', url: '', summary: '', isResolved: true },
      { id: 't2', url: '', summary: '', isResolved: false },
      { id: 't3', url: '', summary: '', isResolved: false },
    ],
    mergeability: { mergeable: true, state: 'unstable' },
  }));
  assert.equal(counted.checksPassed, 1);
  assert.equal(counted.checksFailed, 1);
  assert.equal(counted.checksRunning, 1);
  assert.equal(counted.unresolvedThreads, 2);
  assert.equal(counted.mergeState, 'unstable');
  assert.equal(counted.proposalNumber, '48');
});

test('countForge with no reading is the empty standing', () => {
  assert.deepEqual(countForge(null), noForge);
  assert.deepEqual(countForge(undefined), noForge);
});

test('a role composes to a builder or a reviewer from its trigger and termination', () => {
  const builder = composeStanding(
    member,
    session({ role: { prompt: 'p', tools: ['enqueue_commit'], trigger: 'IssueAssigned', termination: 'ProposalOpened' } }),
    null,
  );
  const reviewer = composeStanding(
    member,
    session({ role: { prompt: 'p', tools: ['play_validation_flow'], trigger: 'ProposalAwaitingReview', termination: 'ReviewSubmitted' } }),
    null,
  );
  assert.equal(builder.role, 'developer');
  assert.deepEqual(builder.roleTools, ['enqueue_commit']);
  assert.equal(reviewer.role, 'reviewer');
});

test('a pairing neither archetype covers is not forced into one', () => {
  assert.equal(classifyRole({ prompt: 'p', tools: [], trigger: 'IssueAssigned', termination: 'ReviewSubmitted' }), 'other');
  assert.equal(classifyRole(null), 'none');
  assert.equal(classifyRole(undefined), 'none');
});

test('no role configured composes as no role rather than a developer', () => {
  const standing = composeStanding(member, session(), null);
  assert.equal(standing.role, 'none');
  assert.deepEqual(standing.roleTools, []);
});

test('the account the workspace acts as comes through the forge read', () => {
  const standing = composeStanding(member, null, hosting({
    account: { login: 'lucy-aup[bot]', displayName: 'Lucy', isBot: true, url: 'https://github.com/apps/lucy-aup' },
  }));
  assert.equal(standing.identity?.login, 'lucy-aup[bot]');
  assert.equal(standing.identity?.isBot, true);
});

test('a host that names no account leaves the identity unset', () => {
  assert.equal(composeStanding(member, null, hosting()).identity, null);
  assert.equal(composeStanding(member, null, null).identity, null);
});

test('the agent is named the way its capability names itself', () => {
  const named = composeStanding(
    member,
    session({ agent: 'claude', agents: [{ agent: 'claude', available: true, displayName: 'Claude' }] }),
    null,
  );
  assert.equal(named.agent, 'Claude');
});

test('the module id stands in when no descriptor names the agent', () => {
  assert.equal(composeStanding(member, session({ agent: 'codex', agents: [] }), null).agent, 'codex');
  assert.equal(composeStanding(member, session({ agent: null, agents: [] }), null).agent, null);
});
