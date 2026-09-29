import { cloneDefinition, type FakeServerDefinition } from '../models/FakeServerDefinition';
import { fakeServerEventFrame, type FakeServerEvent } from '../models/FakeServerEvent';
import { fakeServerId, fakeServerUrl, isFakeServerUrl } from '../models/FakeServerIdentity';
import {
  addAgentWorkingTreeFile,
  checkoutGitRemote,
  commitGitFiles,
  discardGitFiles,
  fetchGitRemote,
  gitChangesPayload,
  gitDiffPayload,
  gitHeadPayload,
  gitLogPayload,
  gitQueuePayload,
  loadFakeGitState,
  pullGitRemote,
  pushGitRemote,
  switchGitBranch,
  type FakeGitState,
} from '../providers/FakeGitProvider';

export const fakeWorkspaceStartPhaseMs = 1000;

export type FakeBackendRequest = {
  method: string;
  path: string;
  query: string;
  body: string | null;
};

export type FakeBackendResponse = {
  status: number;
  contentType: string;
  body?: string;
  keepOpen?: boolean;
};

export type FakeScheduler = (delayMs: number, work: () => void) => () => void;

type AgentListener = (event: FakeServerEvent) => void;
type WorkspaceListener = (snapshot: string) => void;

type WorkspaceRecord = {
  id: string;
  displayName: string;
  repositoryPath: string;
  worktreePath: string;
  branch: string;
  commit: string;
  state: string;
  healthState?: string;
  applications?: Array<{
    name: string;
    state: string;
    kind?: string;
    page?: string;
    allocatedPorts?: Array<{ allocatedPort: number; protocol?: string; variable?: string }>;
  }>;
};

type CapabilityRecord = {
  id: string;
  version: string;
  displayName: string;
  publisher: string;
  kind: string;
  enabled: boolean;
  state: string;
  canRun: boolean;
  messages: string[];
};

const defaultScheduler: FakeScheduler = (delayMs, work) => {
  const timer = setTimeout(work, delayMs);
  return () => clearTimeout(timer);
};

export class FakeBackendService {
  private readonly template: FakeServerDefinition;
  private readonly schedule: FakeScheduler;
  private state: FakeServerDefinition;
  private readonly agentEvents = new Map<string, FakeServerEvent[]>();
  private readonly agentListeners = new Map<string, AgentListener[]>();
  private readonly workspaceListeners: WorkspaceListener[] = [];
  private readonly git = new Map<string, FakeGitState>();
  private readonly appTemplates = new Map<string, NonNullable<WorkspaceRecord['applications']>>();
  private agentSequence = 0;
  private cancelLifecycle: (() => void) | null = null;

  constructor(definition: FakeServerDefinition, schedule: FakeScheduler = defaultScheduler) {
    this.template = cloneDefinition(definition);
    this.schedule = schedule;
    this.state = cloneDefinition(definition);
    this.hydrate();
  }

  catalog(activeServerId?: string | null) {
    return {
      id: fakeServerId,
      url: fakeServerUrl,
      displayName: this.state.displayName,
      isActive: activeServerId === fakeServerId,
    };
  }

  matches(url: string | null | undefined) {
    return isFakeServerUrl(url);
  }

  reset() {
    this.cancelLifecycleWork();
    this.state = cloneDefinition(this.template);
    this.agentEvents.clear();
    this.agentSequence = 0;
    this.hydrate();
  }

  applicationHtml(allocatedPort: number): string | null {
    const page = this.findPageName(allocatedPort);
    return page ? this.pageHtml(page) : null;
  }

  pageHtml(page: string): string | null {
    return this.state.pages?.[page] ?? null;
  }

  handle(request: FakeBackendRequest): FakeBackendResponse {
    const method = request.method.toUpperCase();
    let path = request.path.replace(/\/+$/, '');
    if (!path) path = '/';

    if (method === 'GET' && path === '/api/auth/status') return json(this.state.authentication);
    if (method === 'POST' && path === '/api/auth/login')
      return json({ authenticationRequired: false, accessToken: 'fake-token' });
    if (method === 'GET' && path === '/api/connection') return json(this.state.connection);
    if (method === 'GET' && path === '/api/entitlements') return json(this.state.entitlements);
    if (method === 'GET' && path === '/api/workspaces') return json(this.publicWorkspaces());
    if (method === 'GET' && path === '/api/workspaces/events')
      return { status: 200, contentType: 'text/event-stream', body: this.workspaceSnapshotSse(), keepOpen: true };
    if (method === 'POST' && path === '/api/workspaces/tutorial/cleanup') return { status: 204, contentType: 'application/json' };
    if (method === 'POST' && path === '/api/source-clones') return this.cloneWorkspace(request.body);
    if (method === 'POST' && path === '/api/apps/tickets') return this.issueTicket(request.body);
    if (method === 'POST' && path === '/api/audit/record') return { status: 204, contentType: 'application/json' };
    if (method === 'GET' && path === '/api/capabilities') return json(this.capabilities());
    if (method === 'POST' && path === '/api/capabilities/enable') return this.enableCapability(request.body);
    if (method === 'POST' && path.startsWith('/api/capabilities/disable/'))
      return this.disableCapability(decodeURIComponent(path.slice('/api/capabilities/disable/'.length)));
    if (method === 'GET' && path.startsWith('/apps/')) return this.appPage(path);

    const workspacePath = parseWorkspacePath(path);
    if (workspacePath) return this.handleWorkspace(method, workspacePath.workspaceId, workspacePath.rest, request);
    return notFound();
  }

  agentEventsAfter(workspaceId: string, after: number): FakeServerEvent[] {
    return (this.agentEvents.get(workspaceId) ?? []).filter(item => item.sequence > after).map(cloneEvent);
  }

  subscribeAgent(workspaceId: string, listener: AgentListener): () => void {
    const listeners = this.agentListeners.get(workspaceId) ?? [];
    listeners.push(listener);
    this.agentListeners.set(workspaceId, listeners);
    return () => {
      const current = this.agentListeners.get(workspaceId) ?? [];
      this.agentListeners.set(workspaceId, current.filter(item => item !== listener));
    };
  }

  subscribeWorkspaces(listener: WorkspaceListener): () => void {
    this.workspaceListeners.push(listener);
    return () => {
      const index = this.workspaceListeners.indexOf(listener);
      if (index >= 0) this.workspaceListeners.splice(index, 1);
    };
  }

  workspaceSnapshotSse(): string {
    return this.workspaces().map(workspace => this.workspaceEventFrame(workspace)).join('');
  }

  private handleWorkspace(method: string, workspaceId: string, rest: string, request: FakeBackendRequest): FakeBackendResponse {
    if (method === 'GET' && rest.length === 0) return this.workspaceJson(workspaceId);
    if (method === 'DELETE' && rest.length === 0) return this.deleteWorkspace(workspaceId);
    if (method === 'POST' && rest === 'start') return this.startWorkspace(workspaceId);
    if (method === 'POST' && rest === 'stop') return this.stopWorkspace(workspaceId);
    if (method === 'GET' && rest === 'overview') return this.overview(workspaceId);
    if (method === 'GET' && rest === 'git/changes') return this.gitChanges(workspaceId);
    if (method === 'GET' && rest === 'git/head') return this.gitHead(workspaceId);
    if (method === 'GET' && rest === 'git/log') return this.gitLog(workspaceId);
    if (method === 'GET' && rest === 'commit-queue') return this.gitQueue(workspaceId);
    if (method === 'GET' && rest.startsWith('git/file')) return this.gitDiff(workspaceId, request.query);
    if (method === 'POST' && rest.startsWith('git/')) return this.gitMutation(workspaceId, rest.slice('git/'.length), request.body);
    if (method === 'GET' && rest === 'agent') return this.agentSession(workspaceId);
    if (method === 'POST' && rest === 'agent') return this.scheduleAgent(workspaceId, request.body);
    if (method === 'POST' && rest === 'agent/messages') return this.sendAgentMessage(workspaceId, request.body);
    if (method === 'GET' && rest === 'agent/events') return this.agentEventStream(workspaceId, request.query);
    if ((method === 'POST' || method === 'DELETE') && rest.startsWith('agent/'))
      return { status: 204, contentType: 'application/json' };
    if (method === 'DELETE' && rest === 'agent') return { status: 204, contentType: 'application/json' };
    if (method === 'GET' && rest.endsWith('/output')) return this.consoleOutput(workspaceId, rest);
    if (method === 'GET' && rest.endsWith('/metrics')) return json([]);
    if (method === 'GET' && rest.endsWith('/validation-flows')) return json([]);
    if (method === 'POST' && rest.includes('/viewer-ticket')) return this.viewerTicket(workspaceId);
    if (method === 'GET' && rest.includes('/database/')) return json([]);
    if (method === 'POST' && rest.includes('/database/')) return json({ rows: [], error: null });
    return notFound();
  }

  private workspaceJson(workspaceId: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    return workspace ? json(this.publicWorkspace(workspace)) : notFound();
  }

  private deleteWorkspace(workspaceId: string): FakeBackendResponse {
    this.state.workspaces = this.state.workspaces.filter(item => workspaceIdOf(item) !== workspaceId);
    this.git.delete(workspaceId);
    this.appTemplates.delete(workspaceId);
    this.publishWorkspaces();
    return { status: 204, contentType: 'application/json' };
  }

  private startWorkspace(workspaceId: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    if (!workspace) return notFound();
    this.cancelLifecycleWork();
    this.applyWorkspacePhase(workspace, 'Starting', undefined, 'Starting');
    this.publishWorkspaces();
    const cancels: Array<() => void> = [];
    const cancelAll = () => cancels.forEach(cancel => cancel());
    this.cancelLifecycle = cancelAll;
    cancels.push(this.schedule(fakeWorkspaceStartPhaseMs, () => {
      const current = this.findWorkspace(workspaceId);
      if (!current || current.state !== 'Starting') return;
      this.applyWorkspacePhase(current, 'Running', 'Checking', 'Checking');
      this.publishWorkspaces();
      cancels.push(this.schedule(fakeWorkspaceStartPhaseMs, () => {
        const ready = this.findWorkspace(workspaceId);
        if (!ready || ready.state !== 'Running') return;
        this.applyWorkspacePhase(ready, 'Running', 'Healthy', 'Running');
        this.publishWorkspaces();
        this.cancelLifecycle = null;
      }));
    }));
    return { status: 204, contentType: 'application/json' };
  }

  private stopWorkspace(workspaceId: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    if (!workspace) return notFound();
    this.cancelLifecycleWork();
    this.applyWorkspacePhase(workspace, 'Stopped', undefined, 'Stopped');
    this.publishWorkspaces();
    return { status: 204, contentType: 'application/json' };
  }

  private overview(workspaceId: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    if (!workspace) return notFound();
    const overview = (this.state.overview?.[workspaceId] ?? {}) as Record<string, unknown>;
    const live = this.publicWorkspace(workspace);
    return json({
      id: workspace.id,
      displayName: workspace.displayName,
      repositoryPath: workspace.repositoryPath,
      worktreePath: workspace.worktreePath,
      branch: workspace.branch,
      commit: workspace.commit,
      state: workspace.state,
      cpuPercent: overview.cpuPercent ?? 0,
      memoryBytes: overview.memoryBytes ?? 0,
      storageBytes: overview.storageBytes ?? 0,
      processCount: overview.processCount ?? 0,
      applicationCount: live.applications?.length ?? 0,
    });
  }

  private gitChanges(workspaceId: string): FakeBackendResponse {
    const git = this.git.get(workspaceId);
    return git ? json(gitChangesPayload(git)) : notFound();
  }

  private gitHead(workspaceId: string): FakeBackendResponse {
    const git = this.git.get(workspaceId);
    return git ? json(gitHeadPayload(git)) : notFound();
  }

  private gitLog(workspaceId: string): FakeBackendResponse {
    const git = this.git.get(workspaceId);
    return git ? json(gitLogPayload(git)) : notFound();
  }

  private gitQueue(workspaceId: string): FakeBackendResponse {
    const git = this.git.get(workspaceId);
    return git ? json(gitQueuePayload(git)) : notFound();
  }

  private gitDiff(workspaceId: string, query: string): FakeBackendResponse {
    const path = queryValue(query, 'path');
    const git = this.git.get(workspaceId);
    if (!path || !git) return notFound();
    const file = gitDiffPayload(git, path);
    return file ? json({ path: file.path, status: file.status, isBinary: false, diff: file.diff }) : notFound();
  }

  private gitMutation(workspaceId: string, action: string, body: string | null): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    const git = this.git.get(workspaceId);
    if (!workspace || !git) return notFound();
    if (action === 'commit') {
      const result = commitGitFiles(git, readJsonStringArray(body, 'files'), readJsonString(body, 'message') ?? '');
      workspace.commit = git.commit;
      return json(result);
    }
    if (action === 'discard') return json(discardGitFiles(git, readJsonStringArray(body, 'files')));
    if (action === 'fetch') return json(fetchGitRemote(git));
    if (action === 'pull') return json(pullGitRemote(git));
    if (action === 'push') return json(pushGitRemote(git));
    if (action === 'branch') {
      const result = switchGitBranch(git, readJsonString(body, 'name') ?? '', readJsonBoolean(body, 'create'));
      if (result.succeeded) workspace.branch = git.branch;
      return json(result);
    }
    if (action === 'checkout') {
      const result = checkoutGitRemote(git, readJsonString(body, 'name') ?? '');
      if (result.succeeded) workspace.branch = git.branch;
      return json(result);
    }
    return json({ found: true, succeeded: true, error: null });
  }

  private agentSession(workspaceId: string): FakeBackendResponse {
    const session = this.state.agents?.[workspaceId]?.session;
    return session === undefined ? notFound() : json(this.liveAgentSession(session));
  }

  private scheduleAgent(workspaceId: string, body: string | null): FakeBackendResponse {
    const stored = this.state.agents?.[workspaceId];
    if (!stored) return notFound();
    const requested = readJsonString(body, 'agent');
    if (!requested) return json(this.liveAgentSession(stored.session));
    const enabled = this.enabledAgentDescriptors();
    const match = enabled.find(item => item.agent.toLowerCase() === requested.toLowerCase());
    if (!match) {
      return {
        status: 409,
        contentType: 'application/json',
        body: JSON.stringify({ title: 'Agent could not be scheduled', detail: `${requested} is not enabled.` }),
      };
    }
    const next = {
      ...(stored.session && typeof stored.session === 'object' ? stored.session as Record<string, unknown> : {}),
      agent: match.agent,
      state: 'ready',
      sessionId: 'demo-agent',
      error: null,
    };
    stored.session = next;
    return json(this.liveAgentSession(next));
  }

  private liveAgentSession(session: unknown) {
    const snapshot = session && typeof session === 'object' ? session as Record<string, unknown> : {};
    const agents = this.enabledAgentDescriptors();
    const current = typeof snapshot.agent === 'string' ? snapshot.agent : null;
    const selected = agents.find(item => item.agent.toLowerCase() === current?.toLowerCase()) ?? agents[0] ?? null;
    return {
      ...snapshot,
      agent: selected?.agent ?? null,
      agents,
    };
  }

  private enabledAgentDescriptors() {
    return this.capabilities()
      .filter(item => item.kind === 'agent' && item.enabled)
      .map(item => ({
        agent: item.id,
        available: item.canRun,
        displayName: item.displayName,
      }));
  }

  private sendAgentMessage(workspaceId: string, body: string | null): FakeBackendResponse {
    const session = this.state.agents?.[workspaceId];
    const git = this.git.get(workspaceId);
    if (!session) return notFound();
    const message = readJsonString(body, 'message') ?? '';
    const added = git ? addAgentWorkingTreeFile(git) : null;
    const snapshot = this.liveAgentSession(session.session);
    this.publishAgent(workspaceId, 'user_message', { text: message });
    this.publishAgent(workspaceId, 'state', { ...snapshot, state: 'running' });
    const text = added
      ? `I added \`${added.path}\` so the storefront can show the weekly harbor special. It is uncommitted in the working tree.`
      : 'Harbor Shop is running locally.';
    this.publishAgent(workspaceId, 'session_update', {
      sessionUpdate: 'agent_message_chunk',
      content: { type: 'text', text },
    });
    this.publishAgent(workspaceId, 'state', { ...snapshot, state: 'ready' });
    return { status: 204, contentType: 'application/json' };
  }

  private agentEventStream(workspaceId: string, query: string): FakeBackendResponse {
    const after = Number(queryValue(query, 'after') ?? '0') || 0;
    const frames = this.agentEventsAfter(workspaceId, after).map(fakeServerEventFrame).join('');
    return { status: 200, contentType: 'text/event-stream', body: frames, keepOpen: true };
  }

  private consoleOutput(workspaceId: string, rest: string): FakeBackendResponse {
    const parts = rest.split('/').filter(Boolean);
    if (parts.length < 3) return json([]);
    const key = `${workspaceId}/${decodeURIComponent(parts[1])}`;
    return json(this.state.console?.[key] ?? []);
  }

  private cloneWorkspace(body: string | null): FakeBackendResponse {
    const repository = readJsonString(body, 'repository') ?? 'https://git.example/demo.git';
    let name = repository.split('/').filter(Boolean).at(-1) ?? 'cloned';
    name = name.replace(/\.git$/i, '');
    const workspace: WorkspaceRecord = {
      id: `cloned-${crypto.randomUUID().replaceAll('-', '').slice(0, 10)}`,
      displayName: name,
      repositoryPath: `/demo/${name}`,
      worktreePath: `/demo/${name}`,
      branch: readJsonString(body, 'branch') ?? 'main',
      commit: 'cloned01',
      state: 'Stopped',
      applications: [],
    };
    this.state.workspaces.push(workspace);
    this.appTemplates.set(workspace.id, []);
    this.publishWorkspaces();
    return { status: 201, contentType: 'application/json', body: JSON.stringify(workspace) };
  }

  private issueTicket(body: string | null): FakeBackendResponse {
    const workspaceId = readJsonString(body, 'workspaceId');
    const port = readJsonNumber(body, 'allocatedPort');
    if (!workspaceId || port === null) return notFound();
    const page = this.findPageName(port);
    if (!page) return notFound();
    return json({
      ticket: 'fake-ticket',
      bootstrapPath: `/apps/${encodeURIComponent(workspaceId)}/${page}`,
      expiresAt: new Date(Date.now() + 30 * 60_000).toISOString(),
    });
  }

  private appPage(path: string): FakeBackendResponse {
    const parts = path.split('/').filter(Boolean);
    const page = parts.length >= 3 ? decodeURIComponent(parts[2]) : '';
    const html = this.pageHtml(page);
    return html ? { status: 200, contentType: 'text/html; charset=utf-8', body: html } : notFound();
  }

  private viewerTicket(workspaceId: string): FakeBackendResponse {
    return json({
      viewerUrl: `${fakeServerUrl}/apps/${encodeURIComponent(workspaceId)}/storefront`,
      expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
    });
  }

  private enableCapability(body: string | null): FakeBackendResponse {
    const id = readJsonString(body, 'id');
    const module = this.capabilities().find(item => item.id === id);
    if (!module) return notFound();
    this.setCapability(module.id, true);
    return json(this.capabilities().find(item => item.id === module.id));
  }

  private disableCapability(id: string): FakeBackendResponse {
    const module = this.capabilities().find(item => item.id === id);
    if (!module) return notFound();
    this.setCapability(id, false);
    return json(this.capabilities().find(item => item.id === id));
  }

  private setCapability(id: string, enabled: boolean) {
    const modules = this.capabilities();
    const index = modules.findIndex(item => item.id === id);
    if (index < 0) return;
    modules[index] = {
      ...modules[index],
      enabled,
      state: enabled ? 'ready' : 'disabled',
      canRun: enabled,
      messages: [],
    };
    this.state.capabilities = modules;
  }

  private findWorkspace(workspaceId: string): WorkspaceRecord | undefined {
    return this.workspaces().find(workspace => workspace.id === workspaceId);
  }

  private findPageName(allocatedPort: number): string | undefined {
    return [...this.appTemplates.values()]
      .flat()
      .find(application => application.allocatedPorts?.some(port => port.allocatedPort === allocatedPort))
      ?.page;
  }

  private workspaces(): WorkspaceRecord[] {
    return this.state.workspaces as WorkspaceRecord[];
  }

  private publicWorkspaces(): WorkspaceRecord[] {
    return this.workspaces().map(workspace => this.publicWorkspace(workspace));
  }

  private publicWorkspace(workspace: WorkspaceRecord): WorkspaceRecord {
    return {
      ...workspace,
      applications: workspace.applications ?? [],
    };
  }

  private capabilities(): CapabilityRecord[] {
    return (this.state.capabilities ?? []) as CapabilityRecord[];
  }

  private hydrate() {
    this.git.clear();
    this.appTemplates.clear();
    for (const workspace of this.workspaces()) {
      this.appTemplates.set(workspace.id, structuredClone(workspace.applications ?? []));
      const loaded = loadFakeGitState(workspace.id, this.state.git?.[workspace.id] as Record<string, unknown> | undefined);
      if (loaded) this.git.set(workspace.id, loaded);
    }
  }

  private applyWorkspacePhase(
    workspace: WorkspaceRecord,
    state: string,
    healthState: string | undefined,
    applicationState: string,
  ) {
    workspace.state = state;
    workspace.healthState = healthState;
    workspace.applications = structuredClone(this.appTemplates.get(workspace.id) ?? []).map(application => ({
      ...application,
      state: applicationState,
    }));
  }

  private cancelLifecycleWork() {
    this.cancelLifecycle?.();
    this.cancelLifecycle = null;
  }

  private publishAgent(workspaceId: string, type: string, payload: unknown) {
    this.agentSequence += 1;
    const added: FakeServerEvent = {
      sequence: this.agentSequence,
      type,
      payload,
      timestamp: new Date().toISOString(),
    };
    const events = this.agentEvents.get(workspaceId) ?? [];
    events.push(added);
    this.agentEvents.set(workspaceId, events);
    (this.agentListeners.get(workspaceId) ?? []).forEach(listener => listener(cloneEvent(added)));
  }

  private publishWorkspaces() {
    const snapshot = this.workspaceSnapshotSse();
    [...this.workspaceListeners].forEach(listener => listener(snapshot));
  }

  private workspaceEventFrame(workspace: WorkspaceRecord): string {
    const applications = (workspace.applications ?? []).map(application => ({
      name: application.name,
      state: application.state,
      portHealth: (application.allocatedPorts ?? []).map(port => ({
        allocatedPort: port.allocatedPort,
        healthState: workspace.healthState ?? application.state,
      })),
    }));
    return `data: ${JSON.stringify({
      workspaceId: workspace.id,
      state: workspace.state,
      healthState: workspace.healthState ?? null,
      applications,
    })}\n\n`;
  }
}

function cloneEvent(event: FakeServerEvent): FakeServerEvent {
  return structuredClone(event);
}

function parseWorkspacePath(path: string): { workspaceId: string; rest: string } | null {
  const prefix = '/api/workspaces/';
  if (!path.startsWith(prefix)) return null;
  const remaining = path.slice(prefix.length);
  if (!remaining || remaining === 'events' || remaining.startsWith('tutorial/')) return null;
  const slash = remaining.indexOf('/');
  if (slash < 0) return { workspaceId: decodeURIComponent(remaining), rest: '' };
  const workspaceId = decodeURIComponent(remaining.slice(0, slash));
  return workspaceId ? { workspaceId, rest: remaining.slice(slash + 1) } : null;
}

function queryValue(query: string, name: string): string | null {
  const trimmed = query.startsWith('?') ? query.slice(1) : query;
  for (const pair of trimmed.split('&').filter(Boolean)) {
    const [key, value = ''] = pair.split('=');
    if (decodeURIComponent(key) === name) return decodeURIComponent(value);
  }
  return null;
}

function parseBody(body: string | null): Record<string, unknown> | null {
  if (!body) return null;
  try {
    const parsed = JSON.parse(body) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed as Record<string, unknown> : null;
  } catch {
    return null;
  }
}

function readJsonString(body: string | null, name: string): string | null {
  const parsed = parseBody(body);
  return typeof parsed?.[name] === 'string' ? parsed[name] : null;
}

function readJsonNumber(body: string | null, name: string): number | null {
  const parsed = parseBody(body);
  return typeof parsed?.[name] === 'number' ? parsed[name] : null;
}

function readJsonBoolean(body: string | null, name: string): boolean {
  const parsed = parseBody(body);
  return parsed?.[name] === true;
}

function readJsonStringArray(body: string | null, name: string): string[] {
  const parsed = parseBody(body);
  return Array.isArray(parsed?.[name]) ? parsed[name].filter((item): item is string => typeof item === 'string') : [];
}

function workspaceIdOf(item: unknown): string | undefined {
  return item && typeof item === 'object' && 'id' in item && typeof item.id === 'string' ? item.id : undefined;
}

function json(body: unknown): FakeBackendResponse {
  return { status: 200, contentType: 'application/json', body: JSON.stringify(body) };
}

function notFound(): FakeBackendResponse {
  return {
    status: 404,
    contentType: 'application/json',
    body: JSON.stringify({ title: 'Not Found', detail: 'The fake Server has no resource at that path.' }),
  };
}
