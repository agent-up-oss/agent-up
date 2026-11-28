import type { QueuedTask, OfferedIssue, ReviewKind } from '../models/TaskQueue';
import { reviewLine, reviewQueue } from './ReviewQueueProvider';
import { issueLabel, repositoryLabel, taskLabel } from '../models/TaskQueue';
import { memberInitials } from '../models/TaskSource';
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
  section: 'overview' | 'chat' | 'git' | 'files' | 'file' | 'settings' | 'application';
  agentOpen: boolean;
  serverLabel: string;
  openWorkspaceId: string | null;
  /** The application the open workspace shows on its application page. */
  applicationName?: string;
  /** The open workspace's branch; empty when it is not on one. */
  openBranch?: string;
  /** The file opened from Files, named in the head of its own page. */
  openFile?: string;
  team: TeamStanding[];
  queue: QueuedTask[];
  working: QueuedTask[];
  offered: OfferedIssue[];
  names: Map<string, string>;
  /** Which of the Tasks board's three queues is open. */
  tasksTab?: TasksTab;
  /** The repository the Tasks page has opened. Empty shows the repository list. */
  repository?: string | null;
  repositories?: { id: string; name: string; host: string; members?: { id: string; name: string; initials: string }[] }[];
  pulls?: { id: string; number: number; title: string; reviews: { kind: string; standing: string; login?: string | null; verdict?: string | null }[] }[];
  forgeTab?: string;
  authTab?: string;
  forgeDetail?: string;
  forgeEndpoint?: string;
  forgeConnected?: boolean;
  memberKind?: string;
  boundAgent?: string | null;
  agentEndpoint?: string;
  agentModel?: string;
  systemPrompt?: string;
  /** The role prompt for this member's type: what the system prompt field shows until it is edited. */
  defaultSystemPrompt?: string;
  customizationPrompt?: string;
  promptOverwrite?: boolean;
  flows: CommandCenterFlowSlot[];
  modules?: CommandCenterModuleSlot[];
  /** Member Settings: the autonomous toggle and this member's agents. */
  chat?: { workspaces: SceneSlotItem[]; fields: SceneSlotItem[] };
  validationsOpen: boolean;
  validationRunning: boolean;
  validationPassed: boolean;
  validationSummary: string;
};

export type TasksTab = 'issues' | 'dev' | 'pm';

export const TASK_SOURCE_REFRESH_MS = 30_000;

/** Whether the draft system prompt is no longer this kind's default. A type change then warns. */
export function systemPromptWasCustomized(draft: string | null | undefined, defaultPrompt: string | null | undefined): boolean {
  const current = draft?.trim() ?? '';
  return current.length > 0 && current !== (defaultPrompt?.trim() ?? '');
}

/** Whether a type tab should do nothing, ask before replacing a customized system prompt, or apply. */
export function memberKindChange(
  requested: string | null | undefined,
  currentKind: string,
  promptOverwrite: boolean,
  draftSystemPrompt: string | null | undefined,
  defaultSystemPrompt: string | null | undefined,
): 'none' | 'confirm' | 'apply' {
  if (!requested || requested === currentKind) return 'none';
  return promptOverwrite || systemPromptWasCustomized(draftSystemPrompt, defaultSystemPrompt)
    ? 'confirm'
    : 'apply';
}

/** System and customization drafts the Save and type-change actions should send. */
export function promptSave(
  fields: Record<string, string> | undefined,
  systemFallback: string,
  customizationFallback: string,
): { systemPrompt: string; customizationPrompt: string } {
  return {
    systemPrompt: fields?.['system-prompt'] ?? systemFallback,
    customizationPrompt: fields?.['customization-prompt'] ?? customizationFallback,
  };
}

/** Whether a member PUT still belongs to the Settings workspace that requested it. */
export function applyMemberIfCurrent(requested: string | null, current: string | null): boolean {
  return requested !== null && requested === current;
}

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
export type CommandCenterSection = CommandCenterSceneInput['section'];

/**
 * Which member page an open section draws, and what opens over it. On a phone an application or a
 * file is a sheet over the member page it was opened from - Apps or Files - so that page stays under
 * it. Wider than a phone nothing overlaps: the section is the page.
 */
export function memberSheetLayers(section: CommandCenterSection, phone: boolean): {
  base: CommandCenterSection;
  inner: 'application' | 'file' | null;
} {
  if (phone && section === 'application') return { base: 'overview', inner: 'application' };
  if (phone && section === 'file') return { base: 'files', inner: 'file' };
  return { base: section, inner: null };
}

export function commandCenterSceneId(input: Pick<CommandCenterSceneInput, 'nav' | 'section' | 'agentOpen'>): string {
  if (!input.agentOpen) return input.nav === 1 ? 'mobile-tasks' : input.nav === 2 ? 'mobile-settings' : 'mobile-team';
  if (input.section === 'chat') return 'mobile-chat';
  if (input.section === 'git') return 'mobile-agent-git';
  if (input.section === 'files') return 'mobile-agent-files';
  if (input.section === 'file') return 'mobile-agent-file';
  if (input.section === 'application') return 'mobile-application';
  if (input.section === 'settings') return 'mobile-agent-settings';
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
    'file-name': input.openFile ? input.openFile.slice(input.openFile.lastIndexOf('/') + 1) : '',
    repository: input.repository ?? '',
    forgeDetail: input.forgeDetail ?? '',
    forgeEndpoint: input.forgeEndpoint ?? '',
    agentEndpoint: input.agentEndpoint ?? '',
    agentModel: input.agentModel ?? '',
    memberId: input.openWorkspaceId ?? input.authTab ?? '',
    systemPrompt: input.systemPrompt || input.defaultSystemPrompt || '',
    customizationPrompt: input.customizationPrompt ?? '',
  };
  const state: Record<string, string> = {
    selected: scene === 'mobile-tasks' ? '1' : scene === 'mobile-settings' ? '2' : '0',
  };
  const tab = input.tasksTab ?? 'issues';
  state.tasksTab = tab;
  state[`${tab}Tab`] = '1';
  state.forgeTab = input.forgeTab ?? 'github';
  const authTab = input.authTab || input.team[0]?.workspaceId || '';
  if (authTab) {
    state.authTab = authTab;
    state.authMember = '1';
  }
  if (input.forgeConnected) state.forgeConnected = '1';
  state.memberKind = input.memberKind || 'Author';
  if (input.promptOverwrite) state.promptOverwrite = '1';
  if (input.boundAgent === 'opencode') state.opencodeSettings = '1';
  if (input.boundAgent === 'ollama') state.ollamaSettings = '1';
  if (input.repository) state.repoOpen = '1';
  else state.repoList = '1';
  if (input.chat && input.chat.fields.length > 0) state.chatSettings = '1';
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
      authMembers: input.team.map(standing => ({
        id: standing.workspaceId,
        state: standing.workspaceId === authTab ? 'au-app-tab--selected' : undefined,
        text: { name: standing.displayName },
      })),
      access: (input.repositories ?? [])
        .find(repository => repository.name === (input.repository ?? ''))
        ?.members
        ?.map(member => ({ id: member.id, text: { initials: member.initials } })) ?? [],
      repos: (input.repositories ?? []).map(repository => ({
        id: repository.id,
        action: 'open-repo',
        text: { name: repository.name, host: repository.host },
        items: {
          access: (repository.members ?? []).map(member => ({
            id: member.id,
            text: { initials: member.initials },
          })),
        },
      })),
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
  const open = input.repository;
  const working = open ? input.working.filter(task => repositoryLabel(task) === open) : input.working;
  const queue = open ? input.queue.filter(task => repositoryLabel(task) === open) : input.queue;
  const face = (workspaceId: string | null, login?: string | null) => {
    const named = workspaceId ? input.names.get(workspaceId) : undefined;
    const source = named || login || '';
    return source ? memberInitials(source) : '';
  };
  return [
    ...working.map(task => row(task.id, 'stop', taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId), face(task.heldByWorkspaceId))),
    ...queue.map(task => row(task.id, 'dequeue', taskLabel(task), repositoryLabel(task), name(task.heldByWorkspaceId), face(task.heldByWorkspaceId || task.targetWorkspaceId))),
    ...input.offered.map(issue => row(issue.id, 'queue', issueLabel(issue), open ?? '', '', face(null, issue.assigneeLogin))),
  ];
}

/** One kind of reviewer's queue. An open repository reads its pull requests; otherwise the queue. */
function reviews(input: CommandCenterSceneInput, kind: ReviewKind): SceneSlotItem[] {
  if (input.repository && input.pulls) return pullRows(input, kind);
  const name = (id: string | null) => (id ? input.names.get(id) ?? id : '');
  return reviewQueue([...input.working, ...input.queue], kind)
    .map(entry => row(entry.task.id, entry.standing, taskLabel(entry.task), repositoryLabel(entry.task), reviewLine(entry, name(entry.reviewerId))));
}

function pullRows(input: CommandCenterSceneInput, kind: ReviewKind): SceneSlotItem[] {
  return (input.pulls ?? []).flatMap(pull => pull.reviews.filter(review => review.kind === kind).map(review => row(
    `${pull.id}:${kind}`,
    review.standing,
    `#${pull.number} · ${pull.title}`,
    input.repository ?? '',
    review.standing === 'reviewing'
      ? (review.login ?? '')
      : review.standing === 'reviewed'
        ? `${review.verdict ?? ''} by ${review.login ?? ''}`.trim()
        : 'Waiting for a reviewer',
  )));
}

function row(id: string, action: string, title: string, repo: string, agent: string, initials = ''): SceneSlotItem {
  const parts: string[] = [];
  if (agent) parts.push('agent');
  if (initials) parts.push('assignee');
  return { id, action, parts, text: { title, repo, agent, initials } };
}

/** The short status a capability whose software is missing shows on its button. */
function capabilityButtonLabel(module: CommandCenterModuleSlot): string {
  return (module.missing ?? []).includes('nix') ? 'Needs Nix' : 'Not found';
}

export const githubDotCom = 'https://github.com';

/** The host URL the forge field should show: the saved origin, or github.com when this tab is GitHub. */
export function prefillEndpoint(moduleId: string, saved?: string | null): string {
  if (saved && saved.trim()) return saved.trim();
  return moduleId === 'github' ? githubDotCom : '';
}

/** Token and host URL the Save button should send. Save reads every field, not only save-token. */
export function tokenSave(
  fields?: Record<string, string>,
  text?: string,
): { token: string; endpointUrl: string } {
  return {
    token: (fields?.['save-token'] ?? text ?? '').trim(),
    endpointUrl: (fields?.['forge-endpoint'] ?? '').trim(),
  };
}

/** Endpoint, model, and optional token for OpenCode or the in-process PM agent. */
export function agentSettingSave(
  fields?: Record<string, string>,
  text?: string,
): { endpoint: string; model: string; token: string } {
  return {
    endpoint: (fields?.['agent-endpoint'] ?? '').trim(),
    model: (fields?.['agent-model'] ?? '').trim(),
    token: (fields?.['save-agent-settings'] ?? text ?? '').trim(),
  };
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
