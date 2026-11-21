import assert from 'node:assert/strict';
import test from 'node:test';
import type { TeamStanding } from '../models/TeamStanding';
import { commandCenterScene, commandCenterSceneId, commandCenterServerLabel, fleetHeadline, moduleRow, tasksTabFor } from './CommandCenterSceneProvider';

function standing(patch: Partial<TeamStanding> & Pick<TeamStanding, 'workspaceId' | 'displayName' | 'kind'>): TeamStanding {
  return {
    branch: 'feat/44-restock',
    state: 'Running',
    agent: null,
    taskNumber: null,
    taskTitle: null,
    reason: null,
    diagnosticCode: null,
    permissionRuleIds: [],
    forge: {
      availability: 'ready',
      proposalNumber: null,
      proposalTitle: null,
      proposalUrl: null,
      checksPassed: 0,
      checksFailed: 0,
      checksRunning: 0,
      unresolvedThreads: 0,
      mergeState: null,
      detail: null,
      checksSkipped: 0,
      checksTotal: 0,
      checks: 'none',
      verdicts: [],
    },
    role: 'none',
    roleTools: [],
    identity: null,
    reviews: null,
    slots: [],
    ...patch,
  };
}

test('the phone scene follows the catalog nav and the open agent', () => {
  assert.equal(commandCenterSceneId({ nav: 0, section: 'overview', agentOpen: false }), 'mobile-team');
  assert.equal(commandCenterSceneId({ nav: 1, section: 'overview', agentOpen: false }), 'mobile-tasks');
  assert.equal(commandCenterSceneId({ nav: 2, section: 'overview', agentOpen: false }), 'mobile-settings');
  assert.equal(commandCenterSceneId({ nav: 0, section: 'chat', agentOpen: true }), 'mobile-chat');
  assert.equal(commandCenterSceneId({ nav: 0, section: 'git', agentOpen: true }), 'mobile-agent-git');
  assert.equal(commandCenterSceneId({ nav: 0, section: 'application', agentOpen: true }), 'mobile-application');
});

test('the fleet headline and lanes use the catalog copy and omit an empty lane', () => {
  const team = [
    standing({ workspaceId: 'bob', displayName: 'Bob', kind: 'working', branch: 'feat/45-deliver', taskNumber: 45, taskTitle: 'Deliver' }),
    standing({ workspaceId: 'lucy', displayName: 'Lucy', kind: 'waiting', reason: 'Wait for approval' }),
  ];
  const scene = commandCenterScene({
    surface: 'mobile',
    nav: 0,
    section: 'overview',
    agentOpen: false,
    serverLabel: 'http://localhost:5000',
    openWorkspaceId: null,
    team,
    queue: [],
    working: [],
    offered: [],
    names: new Map(),
    flows: [],
    validationsOpen: false,
    validationRunning: false,
    validationPassed: false,
    validationSummary: 'No check selected',
  });

  assert.equal(fleetHeadline(team), '2 agents · 1 working · 1 waiting for approval');
  assert.equal(scene.text.headline, '2 agents · 1 working · 1 waiting for approval');
  assert.deepEqual(scene.items.fleet.map(lane => lane.text?.title), ['Wait for approval', 'Working']);
  assert.equal(scene.items.fleet[0]?.text?.meta, 'Needs your decision');
  assert.equal(scene.items.fleet[0]?.items?.hosts[0]?.text?.name, 'Lucy');
  assert.equal(scene.items.fleet[0]?.items?.hosts[0]?.parts?.includes('messages'), true);
});

test('a task row names dequeue and the dock reports a run without a timer', () => {
  const scene = commandCenterScene({
    surface: 'mobile',
    nav: 1,
    section: 'application',
    agentOpen: true,
    serverLabel: 'Demo',
    openWorkspaceId: 'bob',
    team: [standing({ workspaceId: 'bob', displayName: 'Bob', kind: 'working' })],
    queue: [{
      id: 'task-1',
      owner: 'shop',
      repository: 'store',
      number: 45,
      title: 'Deliver',
      url: 'https://example/45',
      targetWorkspaceId: null,
      heldByWorkspaceId: 'bob',
    }],
    working: [],
    offered: [],
    names: new Map([['bob', 'Bob']]),
    flows: [{ id: 'checkout', name: 'Checkout featured goods', description: 'Starting page' }],
    validationsOpen: true,
    validationRunning: true,
    validationPassed: false,
    validationSummary: 'Checkout featured goods',
  });

  assert.equal(scene.scene, 'mobile-application');
  assert.equal(scene.items.queue[0]?.action, 'dequeue');
  assert.equal(scene.items.queue[0]?.text?.agent, 'Bob');
  assert.equal(scene.items.flows[0]?.text?.name, 'Checkout featured goods');
  assert.equal(scene.state.open, '1');
  assert.equal(scene.state.running, '1');
  assert.equal(scene.text.summary, 'Checkout featured goods');
});

test('the Demo is named rather than shown by its internal address', () => {
  assert.equal(commandCenterServerLabel({ url: 'http://127.0.0.1:9', displayName: 'Demo', isFake: true }), 'Demo');
  assert.equal(commandCenterServerLabel({ url: 'http://127.0.0.1:9', isFake: true }), 'Demo');
  assert.equal(commandCenterServerLabel({ url: 'https://agent-up.example.com' }), 'https://agent-up.example.com');
  assert.equal(commandCenterServerLabel(null), 'No server');
});

test('an open agent names itself and its branch in the head', () => {
  const input = {
    surface: 'mobile' as const, nav: 0 as const, section: 'overview' as const, agentOpen: true, serverLabel: 'Demo',
    openWorkspaceId: 'bob', team: [standing({ workspaceId: 'bob', displayName: 'Bob', kind: 'working' })],
    queue: [], working: [], offered: [], names: new Map<string, string>(), flows: [],
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  };

  assert.equal(commandCenterScene({ ...input, openBranch: 'feat/45-deliver' }).text.member, 'Bob');
  assert.equal(commandCenterScene({ ...input, openBranch: 'feat/45-deliver' }).text.branch, 'feat/45-deliver');
  assert.equal(commandCenterScene(input).text.branch, 'not on a git branch');
  assert.equal(commandCenterScene({ ...input, section: 'application', applicationName: 'Storefront' }).text['application-name'], 'Storefront');
});

test('the board hands over every row with the action it offers, in no particular grouping', () => {
  const task = (id: string, number: number, held: string | null) => ({
    id, owner: 'shop', repository: 'store', number, title: `Task ${number}`, url: `https://example/${number}`,
    targetWorkspaceId: null, heldByWorkspaceId: held,
  });
  const scene = commandCenterScene({
    surface: 'mobile', nav: 1, section: 'overview', agentOpen: false, serverLabel: 'Demo', openWorkspaceId: null,
    team: [], queue: [task('q1', 51, null)], working: [task('w1', 44, 'bob')],
    offered: [{ id: 'o1', number: 57, title: 'Cart badge', url: 'u' }], names: new Map([['bob', 'Bob']]), flows: [],
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });

  assert.deepEqual(scene.items.queue.map(item => [item.id, item.action]), [['w1', 'stop'], ['q1', 'dequeue'], ['o1', 'queue']]);
  assert.deepEqual(scene.items.queue.map(item => item.text?.agent), ['Bob', '', '']);
});

// The Tasks board has three queues; the open one is the scene's state, and each review queue is
// that kind's own: a held task in review is in the dev queue under its reviewer.
test('the board opens one of its three queues and sorts proposals into each kind of review', () => {
  const inReview = {
    id: 'w1', owner: 'shop', repository: 'store', number: 44, title: 'Task 44', url: 'u', targetWorkspaceId: null, heldByWorkspaceId: 'bob',
    stage: 'inReview' as const, proposal: { number: '7', url: 'p', branch: 'agent-up/issue-44', baseBranch: 'main', revision: 1 },
    reviews: [], devReviewerWorkspaceId: 'hector', pmReviewerWorkspaceId: null, needsReview: ['devReviewer' as const, 'pmReviewer' as const],
  };
  const scene = commandCenterScene({
    surface: 'mobile', nav: 1, section: 'overview', agentOpen: false, serverLabel: 'Demo', openWorkspaceId: null,
    team: [], queue: [], working: [inReview], offered: [], names: new Map([['bob', 'Bob'], ['hector', 'Hector']]), flows: [],
    tasksTab: tasksTabFor('tasks-dev-review') ?? 'issues',
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });

  assert.equal(scene.state.tasksTab, 'dev');
  assert.equal(scene.state.devTab, '1');
  assert.equal(scene.state.issuesTab, undefined);
  assert.deepEqual(scene.items.devReview.map(item => [item.action, item.text?.agent]), [['reviewing', 'Hector · revision 1']]);
  assert.deepEqual(scene.items.pmReview.map(item => [item.action, item.text?.agent]), [['waiting', 'Waiting for a reviewer · revision 1']]);
  assert.equal(tasksTabFor('nav-tasks'), null);
});

test('a capability row names its state and the one change it offers; the scene words both', () => {
  const ready = moduleRow({ id: 'dotnet', displayName: '.NET', enabled: true, canRun: true, state: 'Enabled', messages: [] });
  const blocked = moduleRow({ id: 'docker', displayName: 'Docker', enabled: true, canRun: false, state: 'Unavailable', messages: ['Install Nix first.'] });
  const off = moduleRow({ id: 'cursor', displayName: 'Cursor', enabled: false, canRun: false, state: 'Disabled', messages: [] });

  assert.deepEqual([ready.action, ready.parts], ['disable-module', ['ready']]);
  assert.deepEqual([blocked.parts, blocked.text?.why], [['blocked'], 'Install Nix first.']);
  assert.deepEqual([off.action, off.parts], ['enable-module', ['disabled']]);
});

test('a capability whose software was not found offers no Enable and names what is missing', () => {
  const noNix = moduleRow({ id: 'dotnet', displayName: '.NET', enabled: false, canRun: false, state: 'unavailable', messages: ['Install Nix.'], available: false, missing: ['nix'] });
  const noCli = moduleRow({ id: 'cursor', displayName: 'Cursor', enabled: false, canRun: false, state: 'unavailable', messages: [], available: false, missing: ['agent'] });

  assert.deepEqual([noNix.action, noNix.parts, noNix.text?.why], ['module-unavailable', ['blocked'], 'Needs Nix']);
  assert.equal(noCli.text?.why, 'Not found');
});
