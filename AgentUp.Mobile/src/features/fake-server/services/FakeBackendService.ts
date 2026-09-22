import { cloneDefinition, type FakeServerDefinition } from '../models/FakeServerDefinition';
import { fakeServerEventFrame, type FakeServerEvent } from '../models/FakeServerEvent';
import { fakeServerId, fakeServerUrl, isFakeServerUrl } from '../models/FakeServerIdentity';

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

export class FakeBackendService {
  private readonly template: FakeServerDefinition;
  private state: FakeServerDefinition;
  private readonly agentEvents = new Map<string, FakeServerEvent[]>();
  private readonly agentListeners = new Map<string, AgentListener[]>();
  private readonly workspaceListeners: WorkspaceListener[] = [];
  private agentSequence = 0;

  constructor(definition: FakeServerDefinition) {
    this.template = cloneDefinition(definition);
    this.state = cloneDefinition(definition);
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
    this.state = cloneDefinition(this.template);
    this.agentEvents.clear();
    this.agentSequence = 0;
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
    if (method === 'GET' && path === '/api/workspaces') return json(this.state.workspaces);
    if (method === 'GET' && path === '/api/workspaces/events')
      return { status: 200, contentType: 'text/event-stream', body: this.workspaceSnapshotSse(), keepOpen: true };
    if (method === 'POST' && path === '/api/workspaces/tutorial/cleanup') return { status: 204, contentType: 'application/json' };
    if (method === 'POST' && path === '/api/source-clones') return this.cloneWorkspace(request.body);
    if (method === 'POST' && path === '/api/apps/tickets') return this.issueTicket(request.body);
    if (method === 'POST' && path === '/api/audit/record') return { status: 204, contentType: 'application/json' };
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
    if (method === 'POST' && rest === 'start') return this.setWorkspaceState(workspaceId, 'Running');
    if (method === 'POST' && rest === 'stop') return this.setWorkspaceState(workspaceId, 'Stopped');
    if (method === 'GET' && rest === 'overview') return this.overview(workspaceId);
    if (method === 'GET' && rest === 'git/changes') return this.gitNode(workspaceId, 'changes');
    if (method === 'GET' && rest === 'git/log') return this.gitNode(workspaceId, 'log');
    if (method === 'GET' && rest === 'commit-queue') return this.gitNode(workspaceId, 'queue');
    if (method === 'GET' && rest.startsWith('git/file')) return this.gitDiff(workspaceId, request.query);
    if (method === 'POST' && rest.startsWith('git/')) return this.gitMutation(workspaceId, rest.slice('git/'.length));
    if ((method === 'GET' || method === 'POST') && rest === 'agent') return this.agentSession(workspaceId);
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
    return workspace ? json(workspace) : notFound();
  }

  private deleteWorkspace(workspaceId: string): FakeBackendResponse {
    this.state.workspaces = this.state.workspaces.filter(item => workspaceIdOf(item) !== workspaceId);
    this.publishWorkspaces();
    return { status: 204, contentType: 'application/json' };
  }

  private setWorkspaceState(workspaceId: string, state: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    if (!workspace) return notFound();
    workspace.state = state;
    workspace.applications?.forEach(application => {
      application.state = state === 'Running' ? 'Running' : 'Stopped';
    });
    this.publishWorkspaces();
    return { status: 204, contentType: 'application/json' };
  }

  private overview(workspaceId: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    if (!workspace) return notFound();
    const overview = (this.state.overview?.[workspaceId] ?? {}) as Record<string, unknown>;
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
      applicationCount: workspace.applications?.length ?? 0,
    });
  }

  private gitNode(workspaceId: string, name: string): FakeBackendResponse {
    const node = this.state.git?.[workspaceId]?.[name];
    return node === undefined ? notFound() : json(node);
  }

  private gitDiff(workspaceId: string, query: string): FakeBackendResponse {
    const path = queryValue(query, 'path');
    if (!path) return notFound();
    const diffs = this.state.git?.[workspaceId]?.diffs as Record<string, unknown> | undefined;
    const diff = diffs?.[path];
    return diff === undefined ? notFound() : json(diff);
  }

  private gitMutation(workspaceId: string, action: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    if (!workspace) return notFound();
    if (action === 'commit')
      return json({ found: true, succeeded: true, commit: 'f4ke0001', error: null });
    if (action === 'fetch' || action === 'pull' || action === 'push') {
      return json({
        found: true,
        succeeded: true,
        error: null,
        head: { branch: workspace.branch ?? 'main', localBranches: ['main'], commit: workspace.commit },
      });
    }
    return json({ found: true, succeeded: true, error: null });
  }

  private agentSession(workspaceId: string): FakeBackendResponse {
    const session = this.state.agents?.[workspaceId]?.session;
    return session === undefined ? notFound() : json(session);
  }

  private sendAgentMessage(workspaceId: string, body: string | null): FakeBackendResponse {
    const session = this.state.agents?.[workspaceId];
    if (!session) return notFound();
    const message = readJsonString(body, 'message') ?? '';
    this.publishAgent(workspaceId, 'user_message', { text: message });
    (session.scripts ?? []).flatMap(script => script.events ?? []).forEach(event => {
      if (!event.type || event.payload === undefined) return;
      this.publishAgent(workspaceId, event.type, structuredClone(event.payload));
    });
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

  private findWorkspace(workspaceId: string): WorkspaceRecord | undefined {
    return this.workspaces().find(workspace => workspace.id === workspaceId);
  }

  private findPageName(allocatedPort: number): string | undefined {
    return this.workspaces()
      .flatMap(workspace => workspace.applications ?? [])
      .find(application => application.allocatedPorts?.some(port => port.allocatedPort === allocatedPort))
      ?.page;
  }

  private workspaces(): WorkspaceRecord[] {
    return this.state.workspaces as WorkspaceRecord[];
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
        healthState: 'Healthy',
      })),
    }));
    return `data: ${JSON.stringify({
      workspaceId: workspace.id,
      state: workspace.state,
      healthState: workspace.healthState ?? 'Healthy',
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

function readJsonString(body: string | null, name: string): string | null {
  if (!body) return null;
  try {
    const parsed = JSON.parse(body) as Record<string, unknown>;
    return typeof parsed[name] === 'string' ? parsed[name] : null;
  } catch {
    return null;
  }
}

function readJsonNumber(body: string | null, name: string): number | null {
  if (!body) return null;
  try {
    const parsed = JSON.parse(body) as Record<string, unknown>;
    return typeof parsed[name] === 'number' ? parsed[name] : null;
  } catch {
    return null;
  }
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
