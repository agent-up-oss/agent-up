import {
  cloneDefinition,
  type FakeOfferedIssue,
  type FakeProductDecision,
  type FakeQueuedTask,
  type FakeServerDefinition,
} from '../models/FakeServerDefinition';
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
import { fakeReviewReportStyleTag } from '../providers/FakeReviewReportCss';

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
  derived?: boolean;
  sourceRepository?: string | null;
  sourceRevision?: string | null;
  applications?: Array<{
    name: string;
    state: string;
    kind?: string;
    page?: string;
    database?: boolean;
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

function describeFakeAgent(module: CapabilityRecord) {
  const available = module.enabled && module.canRun;
  if (available) {
    return {
      agent: module.id,
      available: true,
      displayName: module.displayName,
      unavailability: null,
      detail: null,
      remedy: null,
    };
  }

  if (!module.enabled) {
    return {
      agent: module.id,
      available: false,
      displayName: module.displayName,
      unavailability: 'capabilityNotEnabled',
      detail: `${module.displayName} is not enabled on this Server.`,
      remedy: `Enable the ${module.id} capability.`,
    };
  }

  return {
    agent: module.id,
    available: false,
    displayName: module.displayName,
    unavailability: 'executableNotFound',
    detail: `${module.id} ACP executable is not installed or is not on PATH.`,
    remedy: `Install the ${module.id} ACP executable and keep it on PATH.`,
  };
}

const defaultScheduler: FakeScheduler = (delayMs, work) => {
  const timer = setTimeout(work, delayMs);
  return () => clearTimeout(timer);
};

/**
 * Whether this workspace's agent reviews somebody else's work. The role's trigger and termination
 * say so: a proposal awaiting review in, a review out.
 */
function reviewsOthersWork(session: unknown): boolean {
  const role = (session as { role?: { trigger?: string; termination?: string } } | null)?.role;
  return role?.trigger === 'ProposalAwaitingReview' && role?.termination === 'ReviewSubmitted';
}

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
  private loopGeneration = 0;
  private autonomyEnabled = false;
  private readonly permissionPolicies = new Map<string, unknown[]>();
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
    this.autonomyEnabled = false;
    this.permissionPolicies.clear();
    this.loopGeneration += 1;
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
    if (method === 'POST' && path.startsWith('/api/source-clones/') && path.endsWith('/refresh'))
      return this.refreshClone(path, request.body);
    if (method === 'POST' && path === '/api/apps/tickets') return this.issueTicket(request.body);
    if (method === 'POST' && path === '/api/audit/record') return { status: 204, contentType: 'application/json' };
    if (method === 'GET' && path === '/api/autonomy') return json(this.autonomyStatus());
    if (method === 'PUT' && path === '/api/autonomy') {
      this.autonomyEnabled = readJsonBoolean(request.body, 'enabled');
      return { status: 204, contentType: 'application/json' };
    }
    if (method === 'GET' && path === '/api/capabilities') return json(this.capabilities());
    if (method === 'POST' && path === '/api/capabilities/enable') return this.enableCapability(request.body);
    if (method === 'POST' && path.startsWith('/api/capabilities/disable/'))
      return this.disableCapability(decodeURIComponent(path.slice('/api/capabilities/disable/'.length)));
    if (method === 'GET' && path === '/api/tasks') return json(this.taskQueue());
    if (method === 'POST' && path === '/api/tasks') return this.enqueueTask(request.body);
    if (path.startsWith('/api/tasks/')) return this.taskMutation(method, path.slice('/api/tasks/'.length), request.body);
    if (path.startsWith('/api/task-source'))
      return this.taskSource(method, path + (request.query ?? ''), request.body ?? undefined);

    if (path.startsWith('/api/training'))
      return this.training(method, path + (request.query ?? ''), request.body ?? undefined);
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
    if (method === 'GET' && rest === 'review-snapshot') return this.reviewSnapshot(workspaceId);
    if (method === 'GET' && rest === 'git/changes') return this.gitChanges(workspaceId);
    if (method === 'GET' && rest === 'git/head') return this.gitHead(workspaceId);
    if (method === 'GET' && rest === 'git/log') return this.gitLog(workspaceId);
    if (method === 'GET' && rest === 'commit-queue') return this.gitQueue(workspaceId);
    if (method === 'GET' && rest === 'forge') return json(this.forge(workspaceId));
    if (method === 'GET' && rest === 'forge/issues') return json(this.openIssues(workspaceId));
    if (method === 'GET' && rest.startsWith('git/file')) return this.gitDiff(workspaceId, request.query);
    if (method === 'POST' && rest.startsWith('git/')) return this.gitMutation(workspaceId, rest.slice('git/'.length), request.body);
    if (method === 'GET' && rest === 'agent/loop') return this.agentLoop(workspaceId);
    if (method === 'GET' && rest === 'agent/autonomy') return json(this.autonomyStatus(workspaceId));
    if (method === 'GET' && rest === 'agent/policy') return json({ rules: this.permissionPolicies.get(workspaceId) ?? [] });
    if (method === 'PUT' && rest === 'agent/policy') {
      this.permissionPolicies.set(workspaceId, readJsonArray(request.body, 'rules'));
      return { status: 204, contentType: 'application/json' };
    }
    if (method === 'PUT' && rest === 'agent/autonomy') return this.setLoopActivation(workspaceId, request.body);
    if (method === 'GET' && rest === 'agent') return this.agentSession(workspaceId);
    if (method === 'POST' && rest === 'agent') return this.scheduleAgent(workspaceId, request.body);
    if (method === 'PUT' && rest === 'agent/binding') return this.bindAgent(workspaceId, request.body);
    if (method === 'DELETE' && rest === 'agent/binding') return this.unbindAgent(workspaceId);
    if (method === 'PUT' && (rest === 'agent/host-connection' || rest === 'agent/role' || rest === 'agent/queue-order'))
      return { status: 204, contentType: 'application/json' };
    if (method === 'POST' && rest === 'agent/messages') return this.sendAgentMessage(workspaceId, request.body);
    if (method === 'GET' && rest === 'agent/events') return this.agentEventStream(workspaceId, request.query);
    if (method === 'POST' && rest.startsWith('agent/sessions/') && rest.endsWith('/resume'))
      return this.resumeAgent(workspaceId, rest);
    if ((method === 'POST' || method === 'DELETE') && rest.startsWith('agent/'))
      return { status: 204, contentType: 'application/json' };
    if (method === 'DELETE' && rest === 'agent') return { status: 204, contentType: 'application/json' };
    if (method === 'GET' && rest.endsWith('/output')) return this.consoleOutput(workspaceId, rest);
    if (method === 'GET' && rest.endsWith('/metrics')) return json([]);
    if (method === 'GET' && rest.startsWith('validation-flows/'))
      return this.validationFlow(workspaceId, decodeURIComponent(rest.slice('validation-flows/'.length)));
    if (method === 'GET' && (rest === 'validation-flows' || rest.endsWith('/validation-flows')))
      return this.validationFlows(workspaceId, rest, request.query);
    if (method === 'POST' && rest.includes('/viewer-ticket')) return this.viewerTicket(workspaceId);
    const database = parseDatabasePath(rest);
    if (database) return this.databaseRoute(workspaceId, database.application, database.action, method, request);
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

  private reviewSnapshot(workspaceId: string): FakeBackendResponse {
    const workspace = this.findWorkspace(workspaceId);
    if (!workspace) return notFound();
    return html(renderFakeReviewReport(workspace));
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

  /**
   * What the repository host reports for this workspace. The Demo answers the same shape a real
   * forge capability returns, so a client fills from it without a demo-only path; a workspace the
   * definition says nothing about reads as no host connected.
   */
  /**
   * The Demo's one repository. Every demo workspace is a worktree of it, which is why one queue
   * serves them all.
   */
  private static readonly demoOwner = 'example';
  private static readonly demoRepository = 'repository';

  /**
   * The picked queue, shared by every workspace. The Demo answers the shape the Server serves, so
   * the Tasks tab arranges it here exactly as it would against a real Server.
   */
  /**
   * The decision backlog the Train tab reads. Verdicts given here stay for the life of the Demo
   * session, so judging a card and coming back to it shows what you said.
   */
  private training(method: string, path: string, body: string | undefined): FakeBackendResponse {
    const rest = path.slice('/api/training'.length);

    if (method === 'GET' && rest === '/repositories') return json(this.decisionRepositories());

    if (method === 'GET' && rest.startsWith('/decisions')) {
      const query = new URLSearchParams(rest.includes('?') ? rest.slice(rest.indexOf('?') + 1) : '');
      return json(this.decisionsFor(query.get('owner'), query.get('repository'), query.get('category')));
    }

    if (method === 'POST' && rest.startsWith('/decisions/') && rest.endsWith('/review'))
      return this.reviewDecision(rest.slice('/decisions/'.length, rest.length - '/review'.length), body);

    if (method === 'GET' && rest.startsWith('/evidence/'))
      return this.decisionEvidence(decodeURIComponent(rest.slice('/evidence/'.length)));

    if (method === 'GET' && rest.startsWith('/export')) return this.decisionExport(rest.includes('includePending=true'));

    return notFound();
  }

  /**
   * The Demo's own task source. It starts already pointed at the example repository and signed
   * in, because the Demo is there to show the product working rather than to walk a reader
   * through an OAuth round trip against a host that does not exist.
   */
  private taskSource(method: string, path: string, body: string | undefined): FakeBackendResponse {
    const rest = path.slice('/api/task-source'.length);

    if (method === 'GET' && (rest === '' || rest === '/')) return json(this.describeSource());

    if (method === 'PUT') {
      const asked = body ? (JSON.parse(body) as { moduleId?: string; owner?: string; repository?: string }) : {};
      this.source = {
        moduleId: asked.moduleId?.trim() || 'github',
        owner: asked.owner?.trim() ?? '',
        repository: asked.repository?.trim() ?? '',
        connected: this.source?.connected ?? true,
      };
      return json(this.describeSource());
    }

    if (method === 'DELETE') {
      this.source = null;
      return json(this.describeSource());
    }

    if (method === 'POST' && rest === '/login') {
      if (!this.source) return { status: 409, contentType: 'application/json', body: '{}' };
      this.source = { ...this.source, connected: true };
      return json({ authorizeUrl: 'https://example.test/login/oauth/authorize?demo=1' });
    }

    if (method === 'GET' && rest.startsWith('/issues')) {
      // The Demo's issues belong to the example repository, which is the first workspace's.
      const first = (this.state.workspaces[0] as { id?: string } | undefined)?.id;
      return json(this.source?.connected && first ? this.openIssues(first) : []);
    }

    return notFound();
  }

  private describeSource() {
    const source = this.source;
    return {
      moduleId: source?.moduleId ?? null,
      owner: source?.owner ?? null,
      repository: source?.repository ?? null,
      configured: !!source && !!source.owner && !!source.repository,
      connected: !!source?.connected,
      canConnect: true,
      detail: source?.connected
        ? `Reading issues from ${source.owner}/${source.repository}.`
        : source
          ? `Sign in to read issues from ${source.owner}/${source.repository}.`
          : 'Name the repository the agents take their work from.',
      modules: [{ moduleId: 'github', displayName: 'GitHub' }],
    };
  }

  private source: { moduleId: string; owner: string; repository: string; connected: boolean } | null = {
    moduleId: 'github',
    owner: 'example',
    repository: 'repository',
    connected: true,
  };

  private decisions(): FakeProductDecision[] {
    return this.state.productDecisions ?? [];
  }

  private decisionRepositories() {
    const byRepository = new Map<string, FakeProductDecision[]>();
    for (const decision of this.decisions()) {
      const key = `${decision.owner}/${decision.repository}`;
      byRepository.set(key, [...(byRepository.get(key) ?? []), decision]);
    }

    return [...byRepository.values()]
      .map(group => ({
        owner: group[0].owner,
        repository: group[0].repository,
        pending: group.filter(decision => decision.verdict === 'pending').length,
        reviewed: group.filter(decision => decision.verdict !== 'pending').length,
        categories: decisionCategoryIds.map(category => ({
          category,
          label: decisionCategoryLabels[category],
          pending: group.filter(d => d.category === category && d.verdict === 'pending').length,
          reviewed: group.filter(d => d.category === category && d.verdict !== 'pending').length,
        })),
      }))
      .sort((left, right) => right.pending - left.pending || left.repository.localeCompare(right.repository));
  }

  private decisionsFor(owner: string | null, repository: string | null, category: string | null) {
    return this.decisions()
      .filter(decision => !owner || decision.owner.toLowerCase() === owner.toLowerCase())
      .filter(decision => !repository || decision.repository.toLowerCase() === repository.toLowerCase())
      .filter(decision => !category || decision.category === category)
      .sort((left, right) =>
        Number(left.verdict !== 'pending') - Number(right.verdict !== 'pending')
        || left.recordedAt.localeCompare(right.recordedAt));
  }

  private reviewDecision(id: string, body: string | undefined): FakeBackendResponse {
    const decision = this.decisions().find(item => item.id === decodeURIComponent(id));
    if (!decision) return notFound();

    const asked = body ? (JSON.parse(body) as { verdict?: string; note?: string | null }) : {};
    const verdict = asked.verdict ?? 'pending';
    if (!['good', 'bad', 'pending'].includes(verdict)) return notFound();

    decision.verdict = verdict;
    decision.note = asked.note?.trim() ? asked.note.trim() : null;
    decision.reviewedAt = verdict === 'pending' ? null : new Date().toISOString();
    return json(decision);
  }

  private decisionEvidence(evidenceId: string): FakeBackendResponse {
    const image = this.state.decisionEvidence?.[evidenceId];
    return image ? json({ evidenceId, mimeType: image.mimeType, base64: image.base64 }) : notFound();
  }

  private decisionExport(includePending: boolean): FakeBackendResponse {
    const lines = this.decisions()
      .filter(decision => includePending || decision.verdict !== 'pending')
      .map(decision => JSON.stringify({
        id: decision.id,
        repository: `${decision.owner}/${decision.repository}`,
        agent: decision.agent,
        category: decision.category,
        headline: decision.headline,
        detail: decision.detail,
        evidence: decision.evidence,
        evidenceImageIds: decision.evidenceImages.map(image => image.evidenceId),
        recordedAt: decision.recordedAt,
        verdict: decision.verdict,
        note: decision.note,
        reviewedAt: decision.reviewedAt,
      }));
    return { status: 200, contentType: 'application/x-ndjson', body: lines.join('\n') + (lines.length ? '\n' : '') };
  }

  private taskQueue(): FakeQueuedTask[] {
    return this.state.taskQueue ?? [];
  }

  /**
   * Open issues on a workspace's repository, minus the ones already queued. A queued issue is not
   * offered again, which is what the Server's own picker does.
   */
  private openIssues(workspaceId: string): FakeOfferedIssue[] {
    const queued = new Set(this.taskQueue().map(task => task.id));
    return (this.state.issues?.[workspaceId] ?? []).filter(issue => !queued.has(issue.id));
  }

  private enqueueTask(body: string | null): FakeBackendResponse {
    const workspaceId = readJsonString(body, 'workspaceId');
    const issueId = readJsonString(body, 'issueId');
    if (!workspaceId || !issueId) return notFound();

    const issue = (this.state.issues?.[workspaceId] ?? []).find(item => item.id === issueId);
    if (!issue) return notFound();

    const task: FakeQueuedTask = {
      id: issue.id,
      owner: FakeBackendService.demoOwner,
      repository: FakeBackendService.demoRepository,
      number: issue.number,
      title: issue.title,
      body: issue.body,
      url: issue.url,
      queuedAt: new Date().toISOString(),
      targetWorkspaceId: readJsonString(body, 'targetWorkspaceId'),
      heldByWorkspaceId: null,
    };
    this.state.taskQueue = [...this.taskQueue(), task];
    return json(task);
  }

  /**
   * Changes to one queued task. The path carries owner and repository the way the Server's routes
   * do, so only the trailing id and verb decide what happens.
   */
  private taskMutation(method: string, rest: string, body: string | null): FakeBackendResponse {
    const segments = rest.split('/').filter(segment => segment.length > 0);
    if (segments.length < 3) return notFound();

    const id = decodeURIComponent(segments[2]);
    const verb = segments[3];
    const task = this.taskQueue().find(item => item.id === id);
    if (!task) return notFound();

    if (method === 'DELETE' && verb === undefined) {
      // A task a workspace is carrying stays, the same refusal the Server gives.
      if (task.heldByWorkspaceId !== null) return { status: 409, contentType: 'application/json' };
      this.state.taskQueue = this.taskQueue().filter(item => item.id !== id);
      return { status: 204, contentType: 'application/json' };
    }

    if (method === 'POST' && verb === 'release') {
      this.state.taskQueue = this.taskQueue().map(item =>
        item.id === id ? { ...item, heldByWorkspaceId: null } : item);
      return { status: 204, contentType: 'application/json' };
    }

    if (method === 'PUT' && verb === 'position') {
      const index = readJsonNumber(body, 'index');
      if (index === null) return notFound();
      this.state.taskQueue = movedTo(this.taskQueue(), id, index);
      return { status: 204, contentType: 'application/json' };
    }

    if (method === 'PUT' && verb === 'target') {
      const workspaceId = readJsonString(body, 'workspaceId');
      this.state.taskQueue = this.taskQueue().map(item =>
        item.id === id ? { ...item, targetWorkspaceId: workspaceId } : item);
      return { status: 204, contentType: 'application/json' };
    }

    return notFound();
  }

  private forge(workspaceId: string): unknown {
    return this.state.forge?.[workspaceId] ?? {
      availability: 'notInstalled',
      moduleId: null,
      proposal: null,
      checks: [],
      threads: [],
      mergeability: null,
      detail: 'No forge capability package is enabled.',
      remedy: 'Run: agent-up capabilities bootstrap',
    };
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

  private autonomyStatus(workspaceId?: string) {
    const agentAvailable = this.agentCatalogDescriptors().some(item => item.available);
    const item = (id: string, satisfied: boolean, remedy: string) => ({
      id,
      satisfied,
      remedy: satisfied ? null : remedy,
    });
    const workspaceItems = workspaceId ? [
      item('workspaceRegistered', true, 'Register the workspace before activating the loop.'),
      item('boundAgent', true, 'Bind an agent to the workspace.'),
      item('permissionRequests', true, 'Bind an agent that supports permission requests.'),
      item('permissionPolicy', (this.permissionPolicies.get(workspaceId)?.length ?? 0) > 0, 'Add a permission policy before activating the loop.'),
      item('gitBranch', true, 'Check out a git branch before activating the loop.'),
      item('unpushedCommits', true, 'Push or remove unpushed commits before activating the loop.'),
      item('commitQueueEdit', true, 'Finish or abort the commit queue edit session.'),
      item('unassignedChanges', true, 'Assign working-tree changes to a queued proposal.'),
    ] : [];
    return {
      enabled: this.autonomyEnabled,
      preconditions: [
        item('forgeCapabilityEnabled', true, 'Run: agent-up capabilities bootstrap'),
        item('hostConnectionAuthorized', true, 'Sign in to your repository host.'),
        item('agentAvailable', agentAvailable, 'Enable an agent capability'),
        item('autonomyEnabled', this.autonomyEnabled, 'Enable autonomy for this Server'),
        ...workspaceItems,
      ],
    };
  }

  private agentLoop(workspaceId: string): FakeBackendResponse {
    const loop = (this.state.agents?.[workspaceId]?.session as { loop?: unknown } | undefined)?.loop;
    return loop === undefined ? notFound() : json(loop);
  }

  private setLoopActivation(workspaceId: string, body: string | null): FakeBackendResponse {
    const stored = this.state.agents?.[workspaceId];
    if (!stored || !stored.session || typeof stored.session !== 'object') return notFound();
    const session = stored.session as Record<string, unknown>;
    const generation = ++this.loopGeneration;
    if (readJsonBoolean(body, 'active')) {
      session.loop = this.loopSnapshot(true, 'running', null, null);
      this.schedule(fakeWorkspaceStartPhaseMs, () => this.completeLoop(workspaceId, generation));
    } else {
      session.loop = this.loopSnapshot(false, 'stopped', 'Cancelled', 'cancelled');
    }
    this.publishAgent(workspaceId, 'state', this.liveAgentSession(session));
    return { status: 204, contentType: 'application/json' };
  }

  private completeLoop(workspaceId: string, generation: number) {
    if (generation !== this.loopGeneration) return;
    const stored = this.state.agents?.[workspaceId];
    const session = stored?.session as { loop?: { active?: boolean } } | undefined;
    if (!session?.loop?.active) return;
    session.loop = this.loopSnapshot(false, 'idle', 'Completed', 'queue_empty');
    this.publishAgent(workspaceId, 'state', this.liveAgentSession(session));
  }

  private loopSnapshot(active: boolean, phase: string, terminal: string | null, stopReason: string | null) {
    return {
      active,
      phase,
      taskId: '12',
      taskNumber: 12,
      taskTitle: 'Restock featured products',
      terminal,
      stopReason,
      blockReason: null,
      permissionRuleIds: [],
    };
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
    const catalog = this.agentCatalogDescriptors();
    const match = catalog.find(item => item.agent.toLowerCase() === requested.toLowerCase() && item.available);
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
      boundAgent: match.agent,
      state: 'ready',
      sessionId: 'demo-agent',
      error: null,
    };
    stored.session = next;
    return json(this.liveAgentSession(next));
  }

  private resumeAgent(workspaceId: string, rest: string): FakeBackendResponse {
    const stored = this.state.agents?.[workspaceId];
    if (!stored) return notFound();
    const prefix = 'agent/sessions/';
    const suffix = '/resume';
    if (rest.length <= prefix.length + suffix.length) return notFound();
    const sessionId = decodeURIComponent(rest.slice(prefix.length, rest.length - suffix.length));
    const sessions = Array.isArray((stored.session as { sessions?: unknown[] } | undefined)?.sessions)
      ? (stored.session as { sessions: Array<Record<string, unknown>> }).sessions
      : [];
    const saved = sessions.find(item => item.sessionId === sessionId);
    if (!saved) return notFound();
    const next = {
      ...(stored.session && typeof stored.session === 'object' ? stored.session as Record<string, unknown> : {}),
      agent: saved.agent,
      sessionId: saved.sessionId,
      state: 'ready',
      error: null,
    };
    stored.session = next;
    return json(this.liveAgentSession(next));
  }

  private bindAgent(workspaceId: string, body: string | null): FakeBackendResponse {
    const stored = this.state.agents?.[workspaceId];
    if (!stored) return notFound();
    const requested = readJsonString(body, 'agent');
    if (!requested) return json(this.liveAgentSession(stored.session));
    const next = {
      ...(stored.session && typeof stored.session === 'object' ? stored.session as Record<string, unknown> : {}),
      boundAgent: requested,
    };
    stored.session = next;
    return json(this.liveAgentSession(next));
  }

  private unbindAgent(workspaceId: string): FakeBackendResponse {
    const stored = this.state.agents?.[workspaceId];
    if (!stored) return notFound();
    const next = {
      ...(stored.session && typeof stored.session === 'object' ? stored.session as Record<string, unknown> : {}),
      boundAgent: null,
    };
    stored.session = next;
    return json(this.liveAgentSession(next));
  }

  private liveAgentSession(session: unknown) {
    const snapshot = session && typeof session === 'object' ? session as Record<string, unknown> : {};
    const agents = this.agentCatalogDescriptors();
    const current = typeof snapshot.agent === 'string' ? snapshot.agent : null;
    const selected = agents.find(item =>
      item.available && item.agent.toLowerCase() === current?.toLowerCase()) ?? null;
    return {
      ...snapshot,
      agent: selected?.agent ?? null,
      agents,
    };
  }

  private agentCatalogDescriptors() {
    return this.capabilities()
      .filter(item => item.kind === 'agent')
      .map(item => describeFakeAgent(item));
  }

  private sendAgentMessage(workspaceId: string, body: string | null): FakeBackendResponse {
    const session = this.state.agents?.[workspaceId];
    const git = this.git.get(workspaceId);
    if (!session) return notFound();
    const message = readJsonString(body, 'message') ?? '';
    // A reviewer has the author's branch checked out to read it. Writing a file into it would put
    // their name on work that is not theirs, so only a role that opens its own proposal touches
    // the tree.
    const reviewing = reviewsOthersWork(session.session);
    const added = git && !reviewing ? addAgentWorkingTreeFile(git) : null;
    const snapshot = this.liveAgentSession(session.session);
    this.publishAgent(workspaceId, 'user_message', { text: message });
    this.publishAgent(workspaceId, 'state', { ...snapshot, state: 'running' });
    const text = added
      ? `I added \`${added.path}\` so the storefront can show the weekly harbor special. It is uncommitted in the working tree.`
      : reviewing
        ? 'I have read the branch for this proposal and left my notes on the thread. The review is submitted.'
        : 'Harbor is running locally.';
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

  private validationFlows(workspaceId: string, rest: string, query: string): FakeBackendResponse {
    let application = queryValue(query, 'application');
    const nested = rest.match(/^applications\/([^/]+)\/validation-flows$/);
    if (!application && nested) application = decodeURIComponent(nested[1]);
    const flows = this.listedValidationFlows(workspaceId).filter(flow =>
      !application || flow.application === application);
    return json(flows);
  }

  private validationFlow(workspaceId: string, id: string): FakeBackendResponse {
    const flow = this.listedValidationFlows(workspaceId).find(item => item.id === id);
    return flow ? json(flow) : notFound();
  }

  private listedValidationFlows(workspaceId: string): Array<ValidationFlowRecord> {
    return (this.state.validationFlows ?? [])
      .filter((item): item is ValidationFlowRecord => isValidationFlow(item) && item.workspaceId === workspaceId);
  }

  private databaseRoute(
    workspaceId: string,
    application: string,
    action: string,
    method: string,
    request: FakeBackendRequest,
  ): FakeBackendResponse {
    if (!this.findApplication(workspaceId, application)?.database) return notFound();
    const catalog = this.databaseCatalog(workspaceId, application);
    if (!catalog) return notFound();
    if (method === 'GET' && action === 'databases') return json({ databases: catalog.names });
    if (method === 'GET' && action === 'tables') {
      const database = queryValue(request.query, 'database') ?? '';
      return json({ tables: catalog.tables?.[database] ?? [] });
    }
    if (method === 'POST' && action === 'query') {
      const database = readJsonString(request.body, 'database') ?? '';
      const table = tableNameFromSql(readJsonString(request.body, 'sql') ?? '');
      const data = table ? catalog.data[database]?.[table] : undefined;
      return json({ columns: data?.columns ?? [], rows: data?.rows ?? [] });
    }
    return notFound();
  }

  private findApplication(workspaceId: string, name: string) {
    return this.findWorkspace(workspaceId)?.applications?.find(item => item.name === name);
  }

  private databaseCatalog(workspaceId: string, application: string): DatabaseCatalog | null {
    const match = (this.state.databases ?? []).find(item =>
      isDatabaseCatalog(item) && item.workspaceId === workspaceId && item.application === application);
    return match && isDatabaseCatalog(match) ? match : null;
  }

  private cloneWorkspace(body: string | null): FakeBackendResponse {
    const repository = readJsonString(body, 'repository') ?? 'https://git.example/demo.git';
    const derived = readJsonBoolean(body, 'derived');
    const revision = readJsonString(body, 'revision') ?? readJsonString(body, 'branch') ?? 'main';
    let name = repository.split('/').filter(Boolean).at(-1) ?? 'cloned';
    name = name.replace(/\.git$/i, '');
    const workspace: WorkspaceRecord = {
      id: `cloned-${crypto.randomUUID().replaceAll('-', '').slice(0, 10)}`,
      displayName: derived ? `Review · ${revision}` : name,
      repositoryPath: `/demo/${name}`,
      worktreePath: `/demo/${name}`,
      branch: revision,
      commit: 'cloned01',
      state: 'Stopped',
      derived,
      sourceRepository: derived ? repository : null,
      sourceRevision: derived ? revision : null,
      applications: [],
    };
    this.state.workspaces.push(workspace);
    this.appTemplates.set(workspace.id, []);
    this.publishWorkspaces();
    return { status: 201, contentType: 'application/json', body: JSON.stringify(workspace) };
  }

  private refreshClone(path: string, body: string | null): FakeBackendResponse {
    const marker = '/api/source-clones/';
    let workspaceId = path.slice(marker.length);
    if (workspaceId.endsWith('/refresh')) workspaceId = workspaceId.slice(0, -'/refresh'.length);
    const workspace = this.findWorkspace(decodeURIComponent(workspaceId));
    if (!workspace?.derived) {
      return {
        status: 400,
        contentType: 'application/json',
        body: JSON.stringify({ detail: 'Only derived review workspaces can be refreshed in place.' }),
      };
    }
    const revision = readJsonString(body, 'revision');
    if (revision) {
      workspace.sourceRevision = revision;
      workspace.branch = revision;
      workspace.displayName = `Review · ${revision}`;
    }
    workspace.commit = 'refreshed';
    this.publishWorkspaces();
    return { status: 200, contentType: 'application/json', body: JSON.stringify(this.publicWorkspace(workspace)) };
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

/**
 * The queue with one task lifted out and put back at a position, the way the Server rewrites it.
 * The position is clamped, so a drag that overshoots either end parks the task there rather than
 * dropping it out of the queue.
 */
function movedTo(queue: FakeQueuedTask[], id: string, index: number): FakeQueuedTask[] {
  const from = queue.findIndex(item => item.id === id);
  if (from < 0) return queue;
  const rest = queue.filter(item => item.id !== id);
  const to = Math.min(Math.max(index, 0), rest.length);
  return [...rest.slice(0, to), queue[from], ...rest.slice(to)];
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

function readJsonArray(body: string | null, name: string): unknown[] {
  const parsed = parseBody(body);
  return Array.isArray(parsed?.[name]) ? structuredClone(parsed[name]) : [];
}

function workspaceIdOf(item: unknown): string | undefined {
  return item && typeof item === 'object' && 'id' in item && typeof item.id === 'string' ? item.id : undefined;
}

/** The three categories, in the order the Train tab shows them. */
const decisionCategoryIds = ['product-vision', 'gui-decisions', 'code-architecture'] as const;

const decisionCategoryLabels: Record<string, string> = {
  'product-vision': 'Product vision',
  'gui-decisions': 'GUI',
  'code-architecture': 'Architecture',
};

function json(body: unknown): FakeBackendResponse {
  return { status: 200, contentType: 'application/json', body: JSON.stringify(body) };
}

function html(body: string): FakeBackendResponse {
  return { status: 200, contentType: 'text/html; charset=utf-8', body };
}

function escapeHtml(value: string): string {
  return value
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function renderFakeReviewReport(workspace: WorkspaceRecord): string {
  const applications = workspace.applications ?? [];
  const appMarkup = applications.map(app => {
    const ports = (app.allocatedPorts ?? []).map(port =>
      `<p class="au-mono">${escapeHtml(port.variable ?? 'port')} = ${port.allocatedPort} (${escapeHtml(port.protocol ?? 'http')})</p>`
    ).join('');
    return `<article class="au-card"><h3>${escapeHtml(app.name)}</h3><p class="au-muted">${escapeHtml(app.kind ?? 'Process')} · ${escapeHtml(app.state)}</p>${ports}</article>`;
  }).join('');
  return `<!DOCTYPE html><html lang="en" class="au-theme"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Export Report — ${escapeHtml(workspace.displayName)}</title>${fakeReviewReportStyleTag()}</head><body class="au-theme"><article class="au-overview au-report"><p class="au-eyebrow">Export Report</p><h1 class="au-page-title">${escapeHtml(workspace.displayName)}</h1><p class="au-muted">${escapeHtml(workspace.state)}</p><p class="au-mono">${escapeHtml(workspace.repositoryPath)}</p><h2>Applications</h2>${appMarkup}<h2>Screenshots</h2><p class="au-muted">No screenshots.</p><h2>Verification</h2><p class="au-muted">Verification could not run for this worktree.</p></article></body></html>`;
}

function notFound(): FakeBackendResponse {
  return {
    status: 404,
    contentType: 'application/json',
    body: JSON.stringify({ title: 'Not Found', detail: 'The fake Server has no resource at that path.' }),
  };
}

type ValidationFlowRecord = {
  id: string;
  workspaceId: string;
  application: string;
  [key: string]: unknown;
};

type DatabaseCatalog = {
  workspaceId: string;
  application: string;
  names: string[];
  tables: Record<string, string[]>;
  data: Record<string, Record<string, { columns: string[]; rows: string[][] }>>;
};

function isValidationFlow(item: unknown): item is ValidationFlowRecord {
  return !!item
    && typeof item === 'object'
    && typeof (item as ValidationFlowRecord).id === 'string'
    && typeof (item as ValidationFlowRecord).workspaceId === 'string'
    && typeof (item as ValidationFlowRecord).application === 'string';
}

function isDatabaseCatalog(item: unknown): item is DatabaseCatalog {
  return !!item
    && typeof item === 'object'
    && typeof (item as DatabaseCatalog).workspaceId === 'string'
    && typeof (item as DatabaseCatalog).application === 'string'
    && Array.isArray((item as DatabaseCatalog).names);
}

function parseDatabasePath(rest: string): { application: string; action: string } | null {
  const match = rest.match(/^applications\/([^/]+)\/database\/(databases|tables|query)$/);
  return match ? { application: decodeURIComponent(match[1]), action: match[2] } : null;
}

function tableNameFromSql(sql: string): string | null {
  const quoted = sql.match(/from\s+"((?:[^"]|"")+)"/i);
  if (quoted) return quoted[1].replaceAll('""', '"');
  const bare = sql.match(/from\s+([A-Za-z_][\w]*)/i);
  return bare?.[1] ?? null;
}
