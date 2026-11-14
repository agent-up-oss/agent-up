import type { QueuedTask, OfferedIssue } from '../models/TaskQueue';
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
  flows: CommandCenterFlowSlot[];
  modules?: CommandCenterModuleSlot[];
  /** Settings · Chat: the team as tabs, and the selected workspace's agent and loop fields. */
  chat?: { workspaces: SceneSlotItem[]; fields: SceneSlotItem[] };
  validationsOpen: boolean;
  validationRunning: boolean;
  validationPassed: boolean;
  validationSummary: string;
};

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

/**
 * The board in the order it is read: what is being worked, then what waits, then what could be
 * picked. The first row of each group carries the group's label; only waiting rows can be dragged.
 */
function tasks(input: CommandCenterSceneInput): SceneSlotItem[] {
  const name = (id: string | null) => (id ? input.names.get(id) ?? '' : '');
  const groups: Array<[string, SceneSlotItem[]]> = [
    ['In progress', input.working.map(task => row(task.id, taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId), 'stop', 'Stop', false))],
    ['Queue', input.queue.map(task => row(task.id, taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId), 'dequeue', 'Dequeue', true))],
    ['Open issues', input.offered.map(issue => row(issue.id, issueLabel(issue), '', '', 'queue', 'Queue', false))],
  ];
  return groups.flatMap(([label, rows]) => rows.map((item, index) => (index === 0
    ? { ...item, parts: ['section', ...(item.parts ?? [])], text: { ...item.text, section: `${label} · ${rows.length}` } }
    : item)));
}

function row(id: string, title: string, repo: string, agent: string, action: string, label: string, grip: boolean): SceneSlotItem {
  const parts: string[] = [];
  if (grip) parts.push('grip');
  if (agent) parts.push('agent');
  const state = action === 'queue' ? 'aun-btn--primary' : 'aun-btn--danger';
  return { id, action, parts, state, text: { title, repo, agent, action: label } };
}

/**
 * A module the Server has enabled and can run is Ready; one it cannot run says why. Enable is the
 * primary action, because it is the one that gives the agents something they lack.
 */
export function moduleRow(module: CommandCenterModuleSlot): SceneSlotItem {
  const why = module.messages.join(' ').trim();
  const state = !module.enabled ? 'Disabled' : module.canRun ? 'Ready' : why || module.state;
  return {
    id: module.id,
    action: module.enabled ? 'disable-module' : 'enable-module',
    parts: module.enabled && module.canRun ? ['healthy'] : [],
    state: module.enabled ? undefined : 'aun-btn--primary',
    text: { name: module.displayName, state, action: module.enabled ? 'Disable' : 'Enable' },
  };
}
