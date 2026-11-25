import assert from 'node:assert/strict';
import test from 'node:test';
import type { TeamStanding } from '../models/TeamStanding';
import { agentSettingSave, commandCenterScene, commandCenterSceneId, commandCenterServerLabel, fleetHeadline, moduleRow, prefillEndpoint, tasksTabFor, tokenSave } from './CommandCenterSceneProvider';

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
  assert.equal(commandCenterSceneId({ nav: 0, section: 'files', agentOpen: true }), 'mobile-agent-files');
  assert.equal(commandCenterSceneId({ nav: 0, section: 'file', agentOpen: true }), 'mobile-agent-file');
  assert.equal(commandCenterSceneId({ nav: 0, section: 'application', agentOpen: true }), 'mobile-application');
  assert.equal(commandCenterSceneId({ nav: 0, section: 'settings', agentOpen: true }), 'mobile-agent-settings');
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
  assert.equal(commandCenterScene({ ...input, section: 'file', openFile: 'apps/storefront/ProductGrid.tsx' }).text['file-name'], 'ProductGrid.tsx');
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
  assert.deepEqual(scene.items.queue.map(item => item.text?.initials), ['BO', '', '']);
  assert.equal(scene.items.queue[0]?.parts?.includes('assignee'), true);
});

test('one assignee is the round initials, from the holder, the target, or the host login', () => {
  const scene = commandCenterScene({
    surface: 'mobile', nav: 1, section: 'overview', agentOpen: false, serverLabel: 'Demo', openWorkspaceId: null,
    team: [],
    queue: [{
      id: 'q1', owner: 'shop', repository: 'store', number: 51, title: 'Task 51', url: 'u',
      targetWorkspaceId: 'frank', heldByWorkspaceId: null,
    }],
    working: [{
      id: 'w1', owner: 'shop', repository: 'store', number: 44, title: 'Task 44', url: 'u',
      targetWorkspaceId: null, heldByWorkspaceId: 'bob',
    }],
    offered: [{ id: 'o1', number: 57, title: 'Cart badge', url: 'u', assigneeLogin: 'hector' }],
    names: new Map([['bob', 'Bob'], ['frank', 'Frank']]), flows: [],
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });

  assert.deepEqual(scene.items.queue.map(item => item.text?.initials), ['BO', 'FR', 'HE']);
});

test('auth tabs are one per team member and the open repository names who can see it', () => {
  const scene = commandCenterScene({
    surface: 'mobile', nav: 2, section: 'overview', agentOpen: false, serverLabel: 'Demo', openWorkspaceId: null,
    team: [
      standing({ workspaceId: 'bob', displayName: 'Bob', kind: 'idle' }),
      standing({ workspaceId: 'frank', displayName: 'Frank', kind: 'idle' }),
    ],
    queue: [], working: [], offered: [], names: new Map(), flows: [],
    authTab: 'frank',
    repository: 'example/repository',
    repositories: [{
      id: 'github/example/repository',
      name: 'example/repository',
      host: 'GitHub',
      members: [{ id: 'bob', name: 'Bob', initials: 'BO' }, { id: 'frank', name: 'Frank', initials: 'FR' }],
    }],
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });

  assert.deepEqual(scene.items.authMembers.map(item => item.text?.name), ['Bob', 'Frank']);
  assert.equal(scene.state.authTab, 'frank');
  assert.deepEqual(scene.items.access.map(item => item.text?.initials), ['BO', 'FR']);
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
  assert.equal(scene.state.repoList, '1');
  assert.equal(tasksTabFor('nav-tasks'), null);
});

test('an open repository shows its pull requests instead of the queue', () => {
  const scene = commandCenterScene({
    surface: 'mobile', nav: 1, section: 'overview', agentOpen: false, serverLabel: 'Demo', openWorkspaceId: null,
    team: [], queue: [], working: [], offered: [], names: new Map(), flows: [],
    repository: 'example/repository',
    repositories: [{ id: 'github/example/repository', name: 'example/repository', host: 'GitHub' }],
    pulls: [{ id: '42', number: 42, title: 'Long names', reviews: [{ kind: 'devReviewer', standing: 'reviewing', login: 'hector' }] }],
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });

  assert.equal(scene.state.repoOpen, '1');
  assert.equal(scene.state.repoList, undefined);
  assert.equal(scene.text.repository, 'example/repository');
  assert.deepEqual(scene.items.devReview.map(item => [item.action, item.text?.agent]), [['reviewing', 'hector']]);
  assert.deepEqual(scene.items.pmReview, []);
});

test('a capability row names its state and the one change it offers; the scene words both', () => {
  const ready = moduleRow({ id: 'dotnet', displayName: '.NET', enabled: true, canRun: true, state: 'Enabled', messages: [] });
  const blocked = moduleRow({ id: 'docker', displayName: 'Docker', enabled: true, canRun: false, state: 'Unavailable', messages: ['Install Nix first.'] });
  const off = moduleRow({ id: 'cursor', displayName: 'Cursor', enabled: false, canRun: false, state: 'Disabled', messages: [] });

  assert.deepEqual([ready.action, ready.parts], ['disable-module', ['ready']]);
  assert.deepEqual([blocked.parts, blocked.text?.why], [['blocked'], 'Install Nix first.']);
  assert.deepEqual([off.action, off.parts], ['enable-module', ['disabled']]);
});

test('GitHub prefills github.com and Save reads the host URL from every field', () => {
  assert.equal(prefillEndpoint('github', null), 'https://github.com');
  assert.equal(prefillEndpoint('forgejo', null), '');
  assert.equal(prefillEndpoint('github', ' https://ghe.example.test '), 'https://ghe.example.test');
  assert.deepEqual(tokenSave({ 'forge-endpoint': ' https://git.example.test ', 'save-token': ' secret ' }, 'ignored'), {
    token: 'secret',
    endpointUrl: 'https://git.example.test',
  });
  assert.deepEqual(agentSettingSave({
    'agent-endpoint': ' http://localhost:11434 ',
    'agent-model': ' llama ',
    'save-agent-settings': ' secret ',
  }, 'ignored'), {
    endpoint: 'http://localhost:11434',
    model: 'llama',
    token: 'secret',
  });
});

test('member settings is a tab on the open team member', () => {
  assert.equal(commandCenterSceneId({ nav: 0, section: 'settings', agentOpen: true }), 'mobile-agent-settings');
  const scene = commandCenterScene({
    surface: 'mobile', nav: 0, section: 'settings', agentOpen: true, serverLabel: 'Demo', openWorkspaceId: 'bob',
    team: [standing({ workspaceId: 'bob', displayName: 'Bob', kind: 'idle' })],
    queue: [], working: [], offered: [], names: new Map(), flows: [],
    memberKind: 'PmReviewer',
    boundAgent: 'ollama',
    agentEndpoint: 'http://127.0.0.1:11434',
    agentModel: 'llama',
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });

  assert.equal(scene.scene, 'mobile-agent-settings');
  assert.equal(scene.state.memberKind, 'PmReviewer');
  assert.equal(scene.state.ollamaSettings, '1');
  assert.equal(scene.text.agentEndpoint, 'http://127.0.0.1:11434');
  assert.equal(scene.text.agentModel, 'llama');
});

test('the settings scene shows the host URL for the selected forge', () => {
  const github = commandCenterScene({
    surface: 'mobile', nav: 2, section: 'overview', agentOpen: false, serverLabel: 'Demo', openWorkspaceId: null,
    team: [], queue: [], working: [], offered: [], names: new Map(), flows: [],
    forgeTab: 'github',
    forgeEndpoint: 'https://github.com',
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });
  const forgejo = commandCenterScene({
    surface: 'mobile', nav: 2, section: 'overview', agentOpen: false, serverLabel: 'Demo', openWorkspaceId: null,
    team: [], queue: [], working: [], offered: [], names: new Map(), flows: [],
    forgeTab: 'forgejo',
    forgeEndpoint: '',
    validationsOpen: false, validationRunning: false, validationPassed: false, validationSummary: '',
  });

  assert.equal(github.state.forgeTab, 'github');
  assert.equal(github.text.forgeEndpoint, 'https://github.com');
  assert.equal(forgejo.state.forgeTab, 'forgejo');
  assert.equal(forgejo.text.forgeEndpoint, '');
});

test('a capability whose software was not found offers no Enable and names what is missing', () => {
  const noNix = moduleRow({ id: 'dotnet', displayName: '.NET', enabled: false, canRun: false, state: 'unavailable', messages: ['Install Nix.'], available: false, missing: ['nix'] });
  const noCli = moduleRow({ id: 'cursor', displayName: 'Cursor', enabled: false, canRun: false, state: 'unavailable', messages: [], available: false, missing: ['agent'] });

  assert.deepEqual([noNix.action, noNix.parts, noNix.text?.why], ['module-unavailable', ['blocked'], 'Needs Nix']);
  assert.equal(noCli.text?.why, 'Not found');
});
