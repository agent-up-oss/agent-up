import assert from 'node:assert/strict';
import test from 'node:test';
import { noForge, type TeamStanding } from '../models/TeamStanding';
import { checksCount, hasStatusRow, queue, headlineFor, identityHandle, roleLabel, showProposalBesideTask, slotGlyph, stateLabel, taskLabel, threadSummary, triage, whoIs } from './TeamStandingService';

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
    reviews: null,
    slots: [],
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
  assert.deepEqual(result.settled.map(item => item.workspaceId), ['shop', 'fresh']);
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
  assert.equal(headlineFor(standing({ kind: 'halted', taskNumber: 51 })), 'Bob stopped working on #51');
  assert.equal(headlineFor(standing({ kind: 'idle' })), 'Bob has nothing in hand');
});

test('a task label pairs the number with the title when there is one', () => {
  assert.equal(taskLabel(standing({ taskNumber: 44, taskTitle: 'Restock' })), '#44 · Restock');
  assert.equal(taskLabel(standing({ taskNumber: 44 })), '#44');
  assert.equal(taskLabel(standing()), null);
});

test('the checks read as passed over total, and the row only shows with a proposal', () => {
  const forge = { ...noForge, proposalNumber: '44', checksPassed: 24, checksTotal: 34 };
  assert.equal(checksCount(standing({ forge })), '(24/34)');
  assert.equal(hasStatusRow(standing({ forge })), true);
  assert.equal(checksCount(standing()), '(0/0)');
  assert.equal(hasStatusRow(standing()), false);
});

test('the status row belongs to the agent who opened the proposal', () => {
  const forge = { ...noForge, proposalNumber: '44', checksPassed: 2, checksTotal: 2 };
  assert.equal(hasStatusRow(standing({ forge, role: 'developer' })), true);
  assert.equal(hasStatusRow(standing({ forge, role: 'reviewer', reviews: 'dev' })), false);
  assert.equal(hasStatusRow(standing({ forge, role: 'reviewer', reviews: 'pm' })), false);
  assert.equal(hasStatusRow(standing({ forge, role: 'other', reviews: 'dev' })),
    false, 'a role that supplies a review is on somebody else\'s branch however it is classified');
  assert.equal(hasStatusRow(standing({ forge })),
    true, 'nothing says this agent reviews, so a proposal on its branch is most likely its own');
});

test('a slot draws its mark, or a dash while nobody has filled it', () => {
  assert.equal(slotGlyph({ kind: 'dev', mark: 'approved' }), '\u2713');
  assert.equal(slotGlyph({ kind: 'dev', mark: 'blocked' }), '\u2717');
  assert.equal(slotGlyph({ kind: 'pm', mark: 'warning' }), '!');
  assert.equal(slotGlyph({ kind: 'pm', mark: 'none' }), '\u2013');
});

test('thread summaries still count what is unresolved', () => {
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


test('the bridge groups put each agent in exactly one place', () => {
  const result = triage([
    standing({ workspaceId: 'gate', kind: 'waiting' }),
    standing({ workspaceId: 'stopped', kind: 'halted' }),
    standing({ workspaceId: 'busy', kind: 'working' }),
    standing({ workspaceId: 'spare', kind: 'idle' }),
  ]);
  const drawn = [...result.waiting, ...result.halted, ...result.settled].map(item => item.workspaceId);
  assert.equal(new Set(drawn).size, drawn.length, 'the bridge must not draw an agent twice');
  assert.deepEqual(drawn.slice().sort(), result.team.map(item => item.workspaceId).slice().sort());
  assert.deepEqual(result.settled.map(item => item.workspaceId), ['busy', 'spare']);
});

test('an empty team settles nobody', () => {
  assert.deepEqual(triage([]).settled, []);
});

test('a reviewer reviews a task rather than working on it', () => {
  const reviewer = { role: 'reviewer' as const, reviews: 'pm' as const, taskNumber: 44 };
  assert.equal(headlineFor(standing({ ...reviewer, kind: 'working' })), 'Bob is reviewing #44');
  assert.equal(headlineFor(standing({ ...reviewer, kind: 'halted' })), 'Bob stopped reviewing #44');
  assert.equal(headlineFor(standing({ kind: 'working', taskNumber: 44 })), 'Bob is working on #44');
  assert.equal(headlineFor(standing({ kind: 'halted', taskNumber: 44 })), 'Bob stopped working on #44');
});

test('the queue is one row per task, naming everyone on it, most urgent first', () => {
  const onTask = (id: string, kind: TeamStanding['kind']) =>
    standing({ workspaceId: id, displayName: id, kind, taskNumber: 44, taskTitle: 'Restock' });
  const result = queue([
    onTask('bob', 'working'),
    onTask('lucy', 'waiting'),
    onTask('hector', 'halted'),
    standing({ workspaceId: 'other', kind: 'working', taskNumber: 51, taskTitle: 'Anchors' }),
    standing({ workspaceId: 'spare', kind: 'idle' }),
  ]);

  assert.equal(result.length, 2, 'three agents on one issue is one piece of work');
  assert.equal(result[0].number, 44);
  assert.equal(result[0].label, '#44 · Restock');
  assert.equal(result[0].kind, 'waiting', 'a task with one agent at a gate needs you');
  assert.deepEqual(result[0].agents.map(agent => agent.workspaceId), ['bob', 'lucy', 'hector']);
  assert.equal(result[1].number, 51);
});

test('the queue leaves out agents holding no task', () => {
  assert.deepEqual(queue([standing()]), []);
});
