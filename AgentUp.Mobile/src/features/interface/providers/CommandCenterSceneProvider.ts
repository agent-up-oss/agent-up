import type { QueuedTask, OfferedIssue, ReviewKind } from '../models/TaskQueue';
import { reviewLine, reviewQueue } from './ReviewQueueProvider';
import { issueLabel, repositoryLabel, taskLabel } from '../models/TaskQueue';
import type { TeamStanding, TeamStandingKind } from '../models/TeamStanding';

export type SceneSlotItem = {
  id?: string;
  action?: string;
  text?: Record<string, string>;
  parts?: string[];
  items?: Record<string, SceneSlotItem[]>;
  state?: string;
};

export type CommandCenterSceneModel = {
  scene: string;
  text: Record<string, string>;
  items: Record<string, SceneSlotItem[]>;
  state: Record<string, string>;
};

export type CommandCenterFlowSlot = { id: string; name: string; description: string };

/** What Settings needs to know about one capability module on the Server. */
export type CommandCenterModuleSlot = {
  id: string;
  displayName: string;
  enabled: boolean;
  canRun: boolean;
  state: string;
  messages: string[];
  /** Whether the Server found the software the capability runs; absent from older Servers, which means yes. */
  available?: boolean;
  missing?: string[];
};

export type CommandCenterSceneInput = {
  surface: 'desktop' | 'mobile';
  nav: 0 | 1 | 2;
  section: 'overview' | 'chat' | 'git' | 'application';
  agentOpen: boolean;
  serverLabel: string;
  openWorkspaceId: string | null;
  /** The application the open workspace shows on its application page. */
  applicationName?: string;
  /** The open workspace's branch; empty when it is not on one. */
  openBranch?: string;
  team: TeamStanding[];
  queue: QueuedTask[];
  working: QueuedTask[];
  offered: OfferedIssue[];
  names: Map<string, string>;
  /** Which of the Tasks board's three queues is open. */
  tasksTab?: TasksTab;
  flows: CommandCenterFlowSlot[];
  modules?: CommandCenterModuleSlot[];
  /** Settings · Chat: the team as tabs, and the selected workspace's agent and loop fields. */
  chat?: { workspaces: SceneSlotItem[]; fields: SceneSlotItem[] };
  validationsOpen: boolean;
  validationRunning: boolean;
  validationPassed: boolean;
  validationSummary: string;
};

export type TasksTab = 'issues' | 'dev' | 'pm';

/** The board's tab a scene action opens, or null when the action is not a tab. */
export function tasksTabFor(action: string): TasksTab | null {
  if (action === 'tasks-issues') return 'issues';
  if (action === 'tasks-dev-review') return 'dev';
  if (action === 'tasks-pm-review') return 'pm';
  return null;
}

const laneCopy: Array<[TeamStandingKind, string, string, string]> = [
  ['waiting', 'Wait for approval', 'Needs your decision', 'aun-state-map__branch--waiting'],
  ['halted', 'Error', 'Needs recovery', 'aun-state-map__branch--halted'],
  ['working', 'Working', 'Moving work forward', 'aun-state-map__branch--working'],
  ['idle', 'Idle', 'Ready for work', 'aun-state-map__branch--idle'],
];

/** The scene id the phone mounts. The catalog page is that scene. */
export function commandCenterSceneId(input: Pick<CommandCenterSceneInput, 'nav' | 'section' | 'agentOpen'>): string {
  if (!input.agentOpen) return input.nav === 1 ? 'mobile-tasks' : input.nav === 2 ? 'mobile-settings' : 'mobile-team';
  if (input.section === 'chat') return 'mobile-chat';
  if (input.section === 'git') return 'mobile-agent-git';
  if (input.section === 'application') return 'mobile-application';
  return 'mobile-agent-overview';
}

/** Standing, the queue, settings, and the open workspace, in the shape the generated scene repeats. */
export function commandCenterScene(input: CommandCenterSceneInput): CommandCenterSceneModel {
  const scene = commandCenterSceneId(input);
  const text: Record<string, string> = {
    headline: fleetHeadline(input.team),
    server: input.serverLabel,
    summary: input.validationSummary,
    'application-name': input.applicationName ?? '',
    member: input.team.find(item => item.workspaceId === input.openWorkspaceId)?.displayName ?? '',
    branch: input.openBranch ? input.openBranch : 'not on a git branch',
  };
  const state: Record<string, string> = {
    selected: scene === 'mobile-tasks' ? '1' : scene === 'mobile-settings' ? '2' : '0',
  };
  const tab = input.tasksTab ?? 'issues';
  state.tasksTab = tab;
  state[`${tab}Tab`] = '1';
  if (input.chat && input.chat.workspaces.length > 0) state.chatSettings = '1';
  if (input.validationsOpen) state.open = '1';
  if (input.validationRunning) state.running = '1';
  if (input.validationPassed) state.passed = '1';
  return {
    scene,
    text,
    state,
    items: {
      fleet: lanes(input.team),
      queue: tasks(input),
      devReview: reviews(input, 'devReviewer'),
      pmReview: reviews(input, 'pmReviewer'),
      modules: (input.modules ?? []).map(moduleRow),
      settingsWorkspaces: input.chat?.workspaces ?? [],
      chatFields: input.chat?.fields ?? [],
      flows: input.flows.map(flow => ({
        id: flow.id,
        action: 'run',
        parts: ['play'],
        text: { name: flow.name, description: flow.description },
      })),
    },
  };
}

/** The Demo's address is an internal stand-in, so it is named; a real Server is its URL. */
export function commandCenterServerLabel(server: { url: string; displayName?: string; isFake?: boolean } | null | undefined): string {
  if (!server) return 'No server';
  if (server.isFake) return server.displayName || 'Demo';
  return server.url;
}

export function fleetHeadline(team: TeamStanding[]): string {
  const count = (kind: TeamStandingKind) => team.filter(item => item.kind === kind).length;
  const working = count('working');
  const idle = count('idle');
  const waiting = count('waiting');
  const halted = count('halted');
  const parts = [`${team.length} ${team.length === 1 ? 'agent' : 'agents'}`];
  if (working) parts.push(`${working} working`);
  if (idle) parts.push(`${idle} idle`);
  if (waiting) parts.push(`${waiting} waiting for approval`);
  if (halted) parts.push(`${halted} ${halted === 1 ? 'error' : 'errors'}`);
  return parts.join(' · ');
}

function lanes(team: TeamStanding[]): SceneSlotItem[] {
  return laneCopy.flatMap(([kind, title, meta, state]) => {
    const members = team.filter(item => item.kind === kind);
    if (!members.length) return [];
    return [{
      id: state,
      state,
      text: { title, meta, count: String(members.length) },
      items: { hosts: members.map(host) },
    }];
  });
}

function host(standing: TeamStanding): SceneSlotItem {
  const text: Record<string, string> = {
    name: standing.displayName,
    task: standing.taskNumber == null ? 'No task in hand' : `#${standing.taskNumber}${standing.taskTitle ? ` · ${standing.taskTitle}` : ''}`,
  };
  const parts: string[] = [];
  if (standing.reason) {
    parts.push('messages');
    text.message = standing.reason;
  }
  return { id: standing.workspaceId, text, parts };
}

/** The board's rows as the Server holds them. The scene groups, labels, and tones them. */
function tasks(input: CommandCenterSceneInput): SceneSlotItem[] {
  const name = (id: string | null) => (id ? input.names.get(id) ?? '' : '');
  return [
    ...input.working.map(task => row(task.id, 'stop', taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId))),
    ...input.queue.map(task => row(task.id, 'dequeue', taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId))),
    ...input.offered.map(issue => row(issue.id, 'queue', issueLabel(issue), '', '')),
  ];
}

/** One kind of reviewer's queue. Review rows carry no action of their own: reviewers take reviews. */
function reviews(input: CommandCenterSceneInput, kind: ReviewKind): SceneSlotItem[] {
  const name = (id: string | null) => (id ? input.names.get(id) ?? id : '');
  return reviewQueue([...input.working, ...input.queue], kind)
    .map(entry => row(entry.task.id, entry.standing, taskLabel(entry.task), repositoryLabel(entry.task), reviewLine(entry, name(entry.reviewerId))));
}

function row(id: string, action: string, title: string, repo: string, agent: string): SceneSlotItem {
  return { id, action, parts: agent ? ['agent'] : [], text: { title, repo, agent } };
}

/** The short status a capability whose software is missing shows on its button. */
function capabilityButtonLabel(module: CommandCenterModuleSlot): string {
  return (module.missing ?? []).includes('nix') ? 'Needs Nix' : 'Not found';
}

/** Which state a capability module is in; the scene words it and picks the button's tone. */
export function moduleRow(module: CommandCenterModuleSlot): SceneSlotItem {
  const available = module.available !== false;
  const why = available ? module.messages.join(' ').trim() || module.state : capabilityButtonLabel(module);
  const part = !available || (module.enabled && !module.canRun) ? 'blocked' : module.enabled ? 'ready' : 'disabled';
  return {
    id: module.id,
    action: module.enabled ? 'disable-module' : available ? 'enable-module' : 'module-unavailable',
    parts: [part],
    text: { name: module.displayName, why },
  };
}
