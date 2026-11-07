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

export type CommandCenterSceneInput = {
  surface: 'desktop' | 'mobile';
  nav: 0 | 1 | 2;
  section: 'overview' | 'chat' | 'git' | 'application';
  agentOpen: boolean;
  serverLabel: string;
  openWorkspaceId: string | null;
  team: TeamStanding[];
  queue: QueuedTask[];
  working: QueuedTask[];
  offered: OfferedIssue[];
  names: Map<string, string>;
  flows: CommandCenterFlowSlot[];
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
    'application-name': input.team.find(item => item.workspaceId === input.openWorkspaceId)?.displayName ?? '',
  };
  const state: Record<string, string> = {
    selected: scene === 'mobile-tasks' ? '1' : scene === 'mobile-settings' ? '2' : '0',
  };
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
      flows: input.flows.map(flow => ({
        id: flow.id,
        action: 'run',
        parts: ['play'],
        text: { name: flow.name, description: flow.description },
      })),
    },
  };
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

function tasks(input: CommandCenterSceneInput): SceneSlotItem[] {
  const name = (id: string | null) => (id ? input.names.get(id) ?? '' : '');
  return [
    ...input.queue.map(task => row(task.id, taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId), 'dequeue', 'Dequeue')),
    ...input.working.map(task => row(task.id, taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId), 'stop', 'Stop')),
    ...input.offered.map(issue => row(issue.id, issueLabel(issue), '', '', 'queue', 'Queue')),
  ];
}

function row(id: string, title: string, repo: string, agent: string, action: string, label: string): SceneSlotItem {
  const parts = ['grip'];
  if (agent) parts.push('agent');
  return { id, action, parts, text: { title, repo, agent, action: label } };
}
