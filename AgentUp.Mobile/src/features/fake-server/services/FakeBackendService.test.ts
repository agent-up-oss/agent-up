import assert from 'node:assert/strict';
import test from 'node:test';
import { loadFakeServerDefinition } from '../providers/FakeServerDefinitionProvider';
import { FakeBackendService, type FakeScheduler } from './FakeBackendService';

function backend(schedule?: FakeScheduler) {
  return new FakeBackendService(loadFakeServerDefinition(), schedule);
}

function queuedBackend() {
  const queue: Array<() => void> = [];
  const service = backend((_ms, work) => {
    queue.push(work);
    return () => {
      const index = queue.indexOf(work);
      if (index >= 0) queue.splice(index, 1);
    };
  });
  return {
    service,
    step() {
      queue.shift()?.();
    },
  };
}

function json(service: FakeBackendService, method: string, path: string, body: string | null = null) {
  return JSON.parse(service.handle({ method, path, query: '', body }).body!);
}

test('authentication and entitlements do not require a password', () => {
  const service = backend();
  const status = json(service, 'GET', '/api/auth/status');
  const login = json(service, 'POST', '/api/auth/login', '{}');
  const entitlements = json(service, 'GET', '/api/entitlements');
  assert.equal(status.authenticationRequired, false);
  assert.equal(login.accessToken, 'fake-token');
  assert.equal(entitlements.edition, 'community');
});

test('lists the bundled Bob workspace', () => {
  const service = backend();
  const workspaces = json(service, 'GET', '/api/workspaces');
  // The definition lists the team in the reverse of the order a client shows them, because each
  // arriving workspace event promotes its workspace to the top. Find Bob rather than taking the
  // first entry: where he sits in the file is not what this is about.
  const bob = workspaces.find((workspace: { id: string }) => workspace.id === 'bob');
  assert.ok(bob, 'the Demo always carries Bob');
  assert.equal(bob.state, 'Running');
});

test('binding records the workspace agent', () => {
  const service = backend();
  const bound = json(service, 'PUT', '/api/workspaces/bob/agent/binding', JSON.stringify({ agent: 'codex' }));
  assert.equal(bound.boundAgent, 'codex');
  const unbound = json(service, 'DELETE', '/api/workspaces/bob/agent/binding');
  assert.equal(unbound.boundAgent, null);
});

test('agent prompts add a working-tree file and say so', () => {
  const service = backend();
  const types: string[] = [];
  const texts: string[] = [];
  const unsubscribe = service.subscribeAgent('bob', event => {
    types.push(event.type);
    const payload = event.payload as { content?: { text?: string } };
    if (payload?.content?.text) texts.push(payload.content.text);
  });
  const result = service.handle({
    method: 'POST',
    path: '/api/workspaces/bob/agent/messages',
    query: '',
    body: JSON.stringify({ message: 'add a promo banner' }),
  });
  unsubscribe();
  const changes = json(service, 'GET', '/api/workspaces/bob/git/changes');
  assert.equal(result.status, 204);
  assert.ok(types.includes('user_message'));
  assert.ok(types.includes('session_update'));
  assert.match(texts.join('\n'), /PromoBanner/);
  assert.equal(changes.fileCount, 3);
});

test('application tickets serve interactive storefront HTML', () => {
  const service = backend();
  const ticket = json(service, 'POST', '/api/apps/tickets', JSON.stringify({ workspaceId: 'bob', allocatedPort: 9100 }));
  const page = service.handle({ method: 'GET', path: ticket.bootstrapPath, query: '', body: null });
  const orders = service.handle({ method: 'GET', path: '/apps/bob/orders-api', query: '', body: null });
  assert.equal(ticket.bootstrapPath, '/apps/bob/storefront');
  assert.match(page.contentType, /^text\/html/);
  assert.match(page.body ?? '', /Place order/);
  assert.match(orders.body ?? '', /Create order/);
  assert.doesNotMatch(orders.body ?? '', /bundled with the client/);
});

test('capability modules list first-party packages and toggle', () => {
  const service = backend();
  const listed = json(service, 'GET', '/api/capabilities');
  const disabled = json(service, 'POST', '/api/capabilities/disable/dotnet');
  const enabled = json(service, 'POST', '/api/capabilities/enable', JSON.stringify({ id: 'dotnet' }));
  assert.deepEqual(listed.map((item: { id: string }) => item.id), ['dotnet', 'docker', 'codex', 'cursor', 'claude']);
  assert.equal(disabled.enabled, false);
  assert.equal(enabled.enabled, true);
  assert.equal(enabled.canRun, true);
});

test('agent list follows enabled agent capabilities', () => {
  const service = backend();
  const listed = json(service, 'GET', '/api/workspaces/bob/agent');
  json(service, 'POST', '/api/capabilities/disable/claude');
  const afterDisable = json(service, 'GET', '/api/workspaces/bob/agent');
  const rejected = service.handle({
    method: 'POST',
    path: '/api/workspaces/bob/agent',
    query: '',
    body: JSON.stringify({ agent: 'claude' }),
  });
  json(service, 'POST', '/api/capabilities/enable', JSON.stringify({ id: 'claude' }));
  const scheduled = json(service, 'POST', '/api/workspaces/bob/agent', JSON.stringify({ agent: 'claude' }));
  assert.deepEqual(listed.agents.map((item: { agent: string }) => item.agent), ['codex', 'cursor', 'claude']);
  const disabled = afterDisable.agents.find((item: { agent: string }) => item.agent === 'claude');
  assert.equal(disabled.available, false);
  assert.equal(disabled.unavailability, 'capabilityNotEnabled');
  assert.match(disabled.remedy, /claude/);
  assert.equal(rejected.status, 409);
  assert.equal(scheduled.agent, 'claude');
});

test('agent session exposes loop phase, task and stop reason', () => {
  const service = backend();
  const session = json(service, 'GET', '/api/workspaces/bob/agent');
  assert.equal(session.loop.phase, 'running');
  assert.equal(session.loop.taskTitle, 'Restock featured products');
  assert.equal(session.loop.stopReason, null);
  assert.equal(session.boundAgent, 'Codex');
});

test('autonomy enable then activate exposes a typed terminal', () => {
  const service = backend((_, work) => {
    work();
    return () => undefined;
  });
  const before = json(service, 'GET', '/api/autonomy');
  assert.equal(before.enabled, false);
  assert.equal(before.preconditions.map((item: { id: string }) => item.id).join(','),
    'forgeCapabilityEnabled,hostConnectionAuthorized,agentAvailable,autonomyEnabled');
  assert.equal(service.handle({ method: 'PUT', path: '/api/autonomy', query: '', body: '{"enabled":true}' }).status, 204);
  assert.equal(json(service, 'GET', '/api/autonomy').enabled, true);
  assert.equal(service.handle({
    method: 'PUT',
    path: '/api/workspaces/bob/agent/autonomy',
    query: '',
    body: '{"active":true}',
  }).status, 204);
  assert.equal(json(service, 'GET', '/api/workspaces/bob/agent/loop').terminal, 'Completed');
  service.handle({
    method: 'PUT',
    path: '/api/workspaces/bob/agent/autonomy',
    query: '',
    body: '{"active":true}',
  });
  assert.equal(service.handle({
    method: 'PUT',
    path: '/api/workspaces/bob/agent/autonomy',
    query: '',
    body: '{"active":false}',
  }).status, 204);
  assert.equal(json(service, 'GET', '/api/workspaces/bob/agent/loop').terminal, 'Cancelled');
});

test('workspace autonomy reflects the saved permission policy', () => {
  const service = backend();
  const status = () => json(service, 'GET', '/api/workspaces/bob/agent/autonomy');

  assert.equal(status().preconditions.find((item: { id: string }) => item.id === 'permissionPolicy').satisfied, false);
  assert.equal(service.handle({
    method: 'PUT', path: '/api/workspaces/bob/agent/policy', query: '',
    body: '{"rules":[{"id":"allow-execute","kind":"Execute","allow":true}]}',
  }).status, 204);
  assert.equal(status().preconditions.find((item: { id: string }) => item.id === 'permissionPolicy').satisfied, true);
  assert.equal(json(service, 'GET', '/api/workspaces/bob/agent/policy').rules.length, 1);
});

test('agent session lists current and prior saved sessions', () => {
  const session = json(backend(), 'GET', '/api/workspaces/bob/agent');
  assert.equal(session.sessions[0].sessionId, 'demo-agent');
  assert.equal(session.sessions[1].sessionId, 'demo-prior');
  assert.equal(session.sessions[1].agent, 'Claude');
});

test('resume makes the saved session current', () => {
  const service = backend();
  const resumed = json(service, 'POST', '/api/workspaces/bob/agent/sessions/demo-prior/resume', '{}');
  assert.equal(resumed.sessionId, 'demo-prior');
  assert.equal(resumed.agent, 'claude');
  assert.equal(resumed.state, 'ready');
  assert.equal(resumed.boundAgent, 'Codex');
  assert.equal(service.handle({
    method: 'POST',
    path: '/api/workspaces/bob/agent/sessions/missing/resume',
    query: '',
    body: '{}',
  }).status, 404);
});

test('resume returns not found when the workspace or path is invalid', () => {
  const service = backend();
  assert.equal(service.handle({
    method: 'POST',
    path: '/api/workspaces/missing/agent/sessions/demo-prior/resume',
    query: '',
    body: '{}',
  }).status, 404);
  assert.equal(service.handle({
    method: 'POST',
    path: '/api/workspaces/bob/agent/sessions/resume',
    query: '',
    body: '{}',
  }).status, 404);
  assert.equal(service.handle({
    method: 'POST',
    path: '/api/workspaces/bob/agent/sessions//resume',
    query: '',
    body: '{}',
  }).status, 404);
});

test('review snapshot exports the bundled workspace as HTML', () => {
  const service = backend();
  const response = service.handle({ method: 'GET', path: '/api/workspaces/bob/review-snapshot', query: '', body: null });
  assert.equal(response.status, 200);
  assert.equal(response.contentType, 'text/html; charset=utf-8');
  assert.match(response.body ?? '', /Bob/);
  assert.match(response.body ?? '', /Storefront/);
  assert.match(response.body ?? '', /Orders API/);
  assert.match(response.body ?? '', /<h2>Screenshots<\/h2>/);
  assert.match(response.body ?? '', /<h2>Verification<\/h2>/);
  assert.equal(service.handle({ method: 'GET', path: '/api/workspaces/missing/review-snapshot', query: '', body: null }).status, 404);
});

test('git head commit discard and fetch mutate the demo tree', () => {
  const service = backend();
  const head = json(service, 'GET', '/api/workspaces/bob/git/head');
  const committed = json(service, 'POST', '/api/workspaces/bob/git/commit', JSON.stringify({
    files: ['apps/storefront/ProductGrid.tsx'],
    message: 'fix(storefront): featured grid',
  }));
  const afterCommit = json(service, 'GET', '/api/workspaces/bob/git/changes');
  const fetched = json(service, 'POST', '/api/workspaces/bob/git/fetch', '{}');
  assert.equal(head.branch, 'feat/44-restock');
  assert.equal(committed.succeeded, true);
  assert.equal(committed.head.ahead, 1);
  assert.equal(committed.head.behind, 1);
  assert.equal(afterCommit.fileCount, 1);
  assert.equal(fetched.head.behind, 1);
});

test('git pull integrates the incoming remote commit only once', () => {
  const service = backend();
  json(service, 'POST', '/api/workspaces/bob/git/fetch', '{}');
  json(service, 'POST', '/api/workspaces/bob/git/pull', '{}');
  const secondFetch = json(service, 'POST', '/api/workspaces/bob/git/fetch', '{}');
  const secondPull = json(service, 'POST', '/api/workspaces/bob/git/pull', '{}');
  const log = json(service, 'GET', '/api/workspaces/bob/git/log');
  assert.equal(secondFetch.head.behind, 0);
  assert.equal(secondPull.head.behind, 0);
  assert.equal(log.commits.filter((entry: { author: string }) => entry.author === 'origin').length, 1);
});

test('start walks starting checking then healthy and stop keeps apps offline', () => {
  const { service, step } = queuedBackend();
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/bob/stop', query: '', body: null }).status, 204);
  const stopped = json(service, 'GET', '/api/workspaces/bob');
  assert.equal(stopped.state, 'Stopped');
  assert.equal(stopped.healthState, undefined);
  assert.equal(stopped.applications.length, 2);
  assert.equal(stopped.applications.every((item: { state: string }) => item.state === 'Stopped'), true);

  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/bob/start', query: '', body: null }).status, 204);
  assert.equal(json(service, 'GET', '/api/workspaces/bob').state, 'Starting');
  step();
  const checking = json(service, 'GET', '/api/workspaces/bob');
  assert.equal(checking.state, 'Running');
  assert.equal(checking.healthState, 'Checking');
  step();
  const healthy = json(service, 'GET', '/api/workspaces/bob');
  assert.equal(healthy.healthState, 'Healthy');
  assert.equal(healthy.applications.length, 2);
});

test('reset restores the bundled workspaces after a clone', () => {
  const service = backend();
  const bundled = json(service, 'GET', '/api/workspaces').length;
  service.handle({
    method: 'POST',
    path: '/api/source-clones',
    query: '',
    body: JSON.stringify({ repository: 'https://git.example/widgets.git' }),
  });
  assert.equal(json(service, 'GET', '/api/workspaces').length, bundled + 1);
  service.reset();
  assert.equal(json(service, 'GET', '/api/workspaces').length, bundled);
});

test('git discard branch and checkout mutate the demo tree', () => {
  const service = backend();
  const discarded = json(service, 'POST', '/api/workspaces/bob/git/discard', JSON.stringify({
    files: ['apps/api/orders.ts'],
  }));
  const created = json(service, 'POST', '/api/workspaces/bob/git/branch', JSON.stringify({
    name: 'topic',
    create: true,
  }));
  const empty = json(service, 'POST', '/api/workspaces/bob/git/branch', JSON.stringify({ name: ' ' }));
  const remote = json(service, 'POST', '/api/workspaces/bob/git/checkout', JSON.stringify({
    name: 'origin/release',
  }));
  assert.equal(discarded.succeeded, true);
  assert.equal(created.head.branch, 'topic');
  assert.equal(empty.succeeded, false);
  assert.equal(remote.head.branch, 'release');
});

test('a derived clone is labelled as a review workspace and can be refreshed in place', () => {
  const service = backend();
  const cloned = json(service, 'POST', '/api/source-clones', JSON.stringify({
    repository: 'https://git.example/widgets.git',
    branch: 'topic',
    revision: 'topic',
    derived: true,
  }));
  assert.equal(cloned.displayName, 'Review · topic');
  assert.equal(cloned.derived, true);
  assert.equal(cloned.sourceRevision, 'topic');
  const refreshed = json(service, 'POST', `/api/source-clones/${cloned.id}/refresh`);
  assert.equal(refreshed.commit, 'refreshed');
  const refused = service.handle({
    method: 'POST',
    path: '/api/source-clones/bob/refresh',
    query: '',
    body: null,
  });
  assert.equal(refused.status, 400);
});

test('refreshing a review workspace can move it onto another revision', () => {
  const service = backend();
  const cloned = json(service, 'POST', '/api/source-clones', JSON.stringify({
    repository: 'https://git.example/widgets.git',
    branch: 'topic',
    revision: 'topic',
    derived: true,
  }));
  const refreshed = json(service, 'POST', `/api/source-clones/${cloned.id}/refresh`, JSON.stringify({ revision: 'release' }));
  assert.equal(refreshed.id, cloned.id);
  assert.equal(refreshed.displayName, 'Review · release');
  assert.equal(refreshed.sourceRevision, 'release');
});

test('git and agent routes on cloned workspaces return not found', () => {
  const service = backend();
  const cloned = json(service, 'POST', '/api/source-clones', '{}');
  const id = cloned.id;
  assert.equal(service.handle({ method: 'GET', path: `/api/workspaces/${id}/git/head`, query: '', body: null }).status, 404);
  assert.equal(service.handle({ method: 'POST', path: `/api/workspaces/${id}/git/fetch`, query: '', body: '{}' }).status, 404);
  assert.equal(json(service, 'GET', `/api/workspaces/${id}/overview`).applicationCount, 0);
  assert.equal(service.handle({
    method: 'POST',
    path: `/api/workspaces/${id}/agent/messages`,
    query: '',
    body: JSON.stringify({ message: 'hi' }),
  }).status, 404);
});

test('missing workspace and capability ids return not found', () => {
  const service = backend();
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/missing/start', query: '', body: null }).status, 404);
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/missing/stop', query: '', body: null }).status, 404);
  assert.equal(service.handle({ method: 'GET', path: '/api/workspaces/missing/agent', query: '', body: null }).status, 404);
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/missing/git/push', query: '', body: '{}' }).status, 404);
  assert.equal(service.handle({ method: 'POST', path: '/api/capabilities/enable', query: '', body: '{}' }).status, 404);
  assert.equal(service.handle({ method: 'POST', path: '/api/capabilities/disable/missing', query: '', body: null }).status, 404);
});

test('start then stop cancels the checking phase', () => {
  const { service, step } = queuedBackend();
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/bob/stop', query: '', body: null }).status, 204);
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/bob/start', query: '', body: null }).status, 204);
  assert.equal(service.handle({ method: 'POST', path: '/api/workspaces/bob/stop', query: '', body: null }).status, 204);
  step();
  assert.equal(json(service, 'GET', '/api/workspaces/bob').state, 'Stopped');
});

test('unknown git action and empty commit bodies stay safe', () => {
  const service = backend();
  const other = json(service, 'POST', '/api/workspaces/bob/git/status', '{}');
  const emptyCommit = json(service, 'POST', '/api/workspaces/bob/git/commit', null);
  const invalid = json(service, 'POST', '/api/workspaces/bob/git/commit', '{"files":"nope"}');
  assert.equal(other.succeeded, true);
  assert.equal(emptyCommit.succeeded, false);
  assert.equal(invalid.succeeded, false);
});

test('forge answers what the definition says the host reports', () => {
  const service = backend();
  const shop = json(service, 'GET', '/api/workspaces/bob/forge');
  const checkout = json(service, 'GET', '/api/workspaces/lucy/forge');
  const docs = json(service, 'GET', '/api/workspaces/hector/forge');
  assert.equal(shop.availability, 'ready');
  assert.equal(shop.proposal.number, '44');
  assert.equal(shop.mergeability.state, 'unstable');
  assert.equal(shop.checks.length, 4, 'one passing, one failing, one running, one skipped');
  assert.equal(shop.checks.filter((check: { conclusion: string }) => check.conclusion === 'skipped').length, 1);
  assert.equal(checkout.proposal.number, '44', 'the reviewer reads the author\'s proposal');
  assert.deepEqual(shop.reviews.map((review: { state: string }) => review.state), ['CHANGES_REQUESTED']);
  assert.equal(docs.availability, 'unreachable');
  assert.ok(docs.detail.includes('502'));
});

test('forge reads as not installed when the definition says nothing', () => {
  const definition = loadFakeServerDefinition();
  delete definition.forge;
  const hosting = json(new FakeBackendService(definition), 'GET', '/api/workspaces/bob/forge');
  assert.equal(hosting.availability, 'notInstalled');
  assert.equal(hosting.proposal, null);
  assert.match(hosting.remedy, /capabilities bootstrap/);
});

test('the agent loop reports the task and what blocks it', () => {
  const service = backend();
  const shop = json(service, 'GET', '/api/workspaces/bob/agent').loop;
  const checkout = json(service, 'GET', '/api/workspaces/lucy/agent').loop;
  const docs = json(service, 'GET', '/api/workspaces/hector/agent').loop;
  assert.equal(shop.taskNumber, 44);
  assert.equal(shop.active, true);
  assert.ok(checkout.blockReason.includes('approve the review'));
  assert.equal(checkout.permissionRuleIds[0], 'forge.submit_review');
  assert.equal(docs.stopReason, 'forge.unreachable');
  assert.equal(docs.active, false);
});

test('messaging a reviewer leaves the author\'s branch alone', () => {
  const service = backend();
  const before = json(service, 'GET', '/api/workspaces/lucy/git/changes').fileCount;
  const texts: string[] = [];
  const unsubscribe = service.subscribeAgent('lucy', event => {
    if (event.type === 'session_update') texts.push(JSON.stringify(event.payload));
  });
  const result = service.handle({
    method: 'POST',
    path: '/api/workspaces/lucy/agent/messages',
    query: '',
    body: JSON.stringify({ message: 'how did it go?' }),
  });
  unsubscribe();
  const after = json(service, 'GET', '/api/workspaces/lucy/git/changes').fileCount;

  // Lucy reviews what Bob built: her tree is his branch checked out to read, so writing a file
  // into it would put her name on his work.
  assert.equal(result.status, 204);
  assert.equal(after, before);
  assert.match(texts.join('\n'), /review/);
  assert.doesNotMatch(texts.join('\n'), /PromoBanner/);
});

test('the demo serves the picked queue in the order it will be worked', () => {
  const service = backend();
  const queue = json(service, 'GET', '/api/tasks');
  assert.deepEqual(queue.map((task: { number: number }) => task.number), [44, 51, 48]);
  assert.equal(queue[0].heldByWorkspaceId, 'bob');
  assert.equal(queue[1].targetWorkspaceId, 'hector');
});

test('the demo offers only the open issues that are not queued yet', () => {
  const service = backend();
  const offered = json(service, 'GET', '/api/workspaces/bob/forge/issues');
  assert.deepEqual(offered.map((issue: { number: number }) => issue.number), [57, 58, 61]);
});

test('picking an issue puts it at the back of the queue and stops offering it', () => {
  const service = backend();
  json(service, 'POST', '/api/tasks', JSON.stringify({ workspaceId: 'bob', issueId: 'I-57' }));
  const queue = json(service, 'GET', '/api/tasks');
  const offered = json(service, 'GET', '/api/workspaces/bob/forge/issues');
  assert.equal(queue[queue.length - 1].number, 57);
  assert.deepEqual(offered.map((issue: { number: number }) => issue.number), [58, 61]);
});

test('a task a workspace is carrying is not removed, but can be released first', () => {
  const service = backend();
  const refused = service.handle({ method: 'DELETE', path: '/api/tasks/example/repository/I-44', query: '', body: null });
  assert.equal(refused.status, 409);

  service.handle({ method: 'POST', path: '/api/tasks/example/repository/I-44/release', query: '', body: null });
  const removed = service.handle({ method: 'DELETE', path: '/api/tasks/example/repository/I-44', query: '', body: null });
  assert.equal(removed.status, 204);
  assert.equal(json(service, 'GET', '/api/tasks').length, 2);
});

test('dragging a task to a position rewrites the order the demo serves', () => {
  const service = backend();
  const moved = service.handle({
    method: 'PUT', path: '/api/tasks/example/repository/I-44/position', query: '',
    body: JSON.stringify({ index: 2 }),
  });

  assert.equal(moved.status, 204);
  assert.deepEqual(
    json(service, 'GET', '/api/tasks').map((task: { number: number }) => task.number),
    [51, 48, 44],
  );
});

test('a position past the end of the queue parks the task at the end', () => {
  const service = backend();
  service.handle({
    method: 'PUT', path: '/api/tasks/example/repository/I-51/position', query: '',
    body: JSON.stringify({ index: 99 }),
  });

  assert.deepEqual(
    json(service, 'GET', '/api/tasks').map((task: { number: number }) => task.number),
    [44, 48, 51],
  );
});

test('a move with no position is refused rather than silently doing nothing', () => {
  // The demo answered 204 to every unknown verb once, so a reorder it never applied still read as
  // a success and the queue simply did not move.
  const service = backend();
  const refused = service.handle({
    method: 'PUT', path: '/api/tasks/example/repository/I-44/position', query: '', body: null,
  });

  assert.equal(refused.status, 404);
});

test('pointing a task at a workspace is served back on the queue', () => {
  const service = backend();
  service.handle({
    method: 'PUT', path: '/api/tasks/example/repository/I-48/target', query: '',
    body: JSON.stringify({ workspaceId: 'lucy' }),
  });

  const task = json(service, 'GET', '/api/tasks').find((item: { number: number }) => item.number === 48);
  assert.equal(task.targetWorkspaceId, 'lucy');
});
