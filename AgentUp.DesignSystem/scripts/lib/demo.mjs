/**
 * The New UI showcase reads `AgentUp.FakeServer/definition.json`, the same document the Demo
 * server serves. Workspace names, the fleet, and the task queue are projected from that document
 * Workspace order matches the clients. An error stands in Error, an approval
 * stands in Wait for approval, an active agent stands in Working, and anyone
 * else stands idle. Empty lanes are not drawn.
 */

import { readFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const definitionPath = resolve(dirname(fileURLToPath(import.meta.url)), '../../../AgentUp.FakeServer/definition.json');

export async function loadDemo() {
  return projectDemo(JSON.parse(await readFile(definitionPath, 'utf8')));
}

/** What the showcase draws, from one Demo definition. */
export function projectDemo(definition) {
  const workspaces = orderWorkspaces(definition.workspaces ?? []);
  const names = new Map(workspaces.map(workspace => [workspace.id, workspace.displayName]));
  const standings = workspaces.map(workspace => standingFor(workspace, definition.agents?.[workspace.id]?.session ?? null));
  const lanes = stateLanes(standings);
  const tasks = taskGroups(standings);
  const queue = definition.taskQueue ?? [];
  const working = queue.filter(task => task.heldByWorkspaceId != null);
  const waiting = queue.filter(task => task.heldByWorkspaceId == null);
  const queuedIds = new Set(queue.map(task => task.id));
  const offering = workspaces[0]?.id ?? null;
  const offered = ((offering && definition.issues?.[offering]) || [])
    .filter(issue => !queuedIds.has(issue.id));
  const waitingCount = standings.filter(item => item.kind === 'waiting').length;
  const haltedCount = standings.filter(item => item.kind === 'halted').length;
  const workingCount = standings.filter(item => item.kind === 'working').length;
  const idleCount = standings.filter(item => item.kind === 'idle').length;
  const sourceTask = queue[0] ?? null;
  const fleet = { agents: standings.length, working: workingCount, idle: idleCount, attention: waitingCount + haltedCount };
  return {
    workspaces,
    open: workspaces[0] ?? null,
    standings,
    lanes,
    tasks,
    working,
    waiting,
    offered,
    names,
    headline: fleetHeadline(standings.length, workingCount, idleCount, waitingCount, haltedCount),
    fleet,
    source: sourceTask ? { owner: sourceTask.owner, repository: sourceTask.repository } : null,
    decisions: definition.productDecisions ?? [],
    overview: definition.overview ?? {},
    flows: validationFor(definition, workspaces[0] ?? null),
  };
}

/**
 * Writes the projected Demo into the New UI components that carry a `data-au-demo` slot.
 * The slot is the only sample those components keep; the words come from the definition.
 */
export function applyDemo(catalog, demo) {
  const byId = new Map();
  for (const surface of catalog.surfaces) {
    for (const component of surface.components) byId.set(component.id, component);
  }
  const fill = (id, name, inner) => {
    const component = byId.get(id);
    if (!component) throw new Error(`New UI catalog is missing '${id}'.`);
    const next = fillSlot(component.html, name, inner);
    if (next === component.html) throw new Error(`${id} is missing demo slot '${name}'.`);
    component.html = next;
  };
  const write = (id, name, value) => {
    const component = byId.get(id);
    if (!component) throw new Error(`New UI catalog is missing '${id}'.`);
    const next = setText(component.html, name, esc(value));
    if (next == null) throw new Error(`${id} is missing data-au-text="${name}".`);
    component.html = next;
  };

  fill('sidebar', 'workspaces', workspaceRows(demo.workspaces, demo.standings, false));
  fill('sidebar-member', 'workspaces', workspaceRows(demo.workspaces, demo.standings, true));
  fill('bridge-heading', 'headline', esc(demo.headline));
  fill('mobile-head-team', 'headline', esc(demo.headline));
  const graph = fleetMap(demo);
  fill('agent-state-map', 'fleet', graph);
  fill('mobile-state-map', 'fleet', graph);
  const lanes = Math.max(demo.lanes.length, 1);
  for (const id of ['agent-state-map', 'mobile-state-map']) {
    const component = byId.get(id);
    component.html = component.html.replace(
      /class="aun-state-map([^"]*)"/,
      `class="aun-state-map$1" style="--aun-lanes: ${lanes}"`,
    );
  }
  write('agent-state-map', 'total', String(demo.fleet.agents));
  write('agent-state-map', 'totalLabel', demo.fleet.agents === 1 ? 'AGENT' : 'AGENTS');
  write('agent-state-map', 'meta', fleetMeta(demo.fleet));
  fill('bridge-task-table', 'reported', reportedTable(demo));
  fill('task-board', 'queue', taskBoard(demo));
  fill('task-source', 'source', taskSource(demo));
  fill('train-panel', 'decisions', decisions(demo));
  fill('member-overview', 'overview', memberOverview(demo));
  const open = demo.open;
  fill('mobile-agent-head', 'member', memberHead(open));
  const application = open?.applications?.[0]?.name ?? '';
  fill('mobile-application-head', 'application-name', `<div data-au-use="page-title" data-au-props="title=${esc(application)}"></div>`);
  fill('application-validation', 'rail', validationRail(demo.flows));
  fill('application-dock', 'dock', validationDock(demo.flows));
}

function orderWorkspaces(workspaces) {
  return workspaces.slice().sort((left, right) => {
    const active = activePriority(right.state) - activePriority(left.state);
    if (active !== 0) return active;
    return left.displayName.localeCompare(right.displayName);
  });
}

function activePriority(state) {
  if (state === 'Running' || state === 'Degraded') return 2;
  if (state === 'Starting') return 1;
  return 0;
}

function standingFor(workspace, session) {
  const loop = session?.loop ?? null;
  const kind = classify(session, loop);
  return {
    id: workspace.id,
    displayName: workspace.displayName,
    kind,
    taskNumber: loop?.taskNumber ?? null,
    taskTitle: loop?.taskTitle ?? null,
    messages: agentMessages(loop),
    role: classifyRole(session?.role),
  };
}

function classify(session, loop) {
  if (!loop) return same(session?.state, 'running') ? 'working' : 'idle';
  if (isHalted(loop)) return 'halted';
  if (isWaiting(session, loop)) return 'waiting';
  return loop.active ? 'working' : 'idle';
}

function isHalted(loop) {
  if (same(loop.phase, 'failed') || same(loop.terminal, 'Failed')) return true;
  return !loop.active && !!loop.stopReason && !same(loop.stopReason, 'queue_empty');
}

function isWaiting(session, loop) {
  return same(loop.phase, 'blocked')
    || same(loop.terminal, 'BlockedAwaitingInput')
    || !!loop.blockReason
    || same(session?.state, 'awaiting_input');
}

function classifyRole(role) {
  if (!role) return null;
  if (role.trigger === 'IssueAssigned' && role.termination === 'ProposalOpened') return 'Developer';
  if (role.trigger === 'ProposalAwaitingReview' && role.termination === 'ReviewSubmitted') return 'Reviewer';
  return 'Custom role';
}

const laneCopy = [
  ['waiting', 'Wait for approval', 'Needs your decision', '◆', 'No decisions waiting'],
  ['halted', 'Error', 'Needs recovery', '▲', 'No stopped agents'],
  ['working', 'Working', 'Moving work forward', '●', 'No active work'],
  ['idle', 'Idle', 'Ready for work', '○', 'No agents standing by'],
];

function stateLanes(standings) {
  return laneCopy.map(([kind, label, description, glyph, emptyLabel]) => ({
    kind,
    label,
    description,
    glyph,
    emptyLabel,
    members: standings.filter(item => item.kind === kind),
  })).filter(lane => lane.members.length > 0);
}

function fleetHeadline(agents, working, idle, waiting, halted) {
  const parts = [`${agents} ${plural(agents, 'agent')}`];
  if (working) parts.push(`${working} working`);
  if (idle) parts.push(`${idle} idle`);
  if (waiting) parts.push(`${waiting} waiting for approval`);
  if (halted) parts.push(`${halted} ${plural(halted, 'error')}`);
  return parts.join(' · ');
}

function fleetMeta(fleet) {
  const parts = [`${fleet.working} active`, `${fleet.idle} ready`];
  if (fleet.attention) parts.push(`${fleet.attention} need attention`);
  return parts.join(' · ');
}

function agentMessages(loop) {
  const seen = new Set();
  const messages = [];
  for (const value of [loop?.blockReason, loop?.stopReason]) {
    if (typeof value !== 'string' || !value.includes(' ')) continue;
    const text = value.trim();
    if (!text || seen.has(text)) continue;
    seen.add(text);
    messages.push(text.length > 100 ? `${text.slice(0, 99).trimEnd()}…` : text);
    if (messages.length === 3) break;
  }
  return messages;
}

function taskGroups(standings) {
  const byTask = new Map();
  for (const standing of standings) {
    if (standing.taskNumber == null) continue;
    const held = byTask.get(standing.taskNumber) ?? [];
    held.push(standing);
    byTask.set(standing.taskNumber, held);
  }
  return [...byTask.entries()]
    .map(([number, agents]) => ({
      number,
      label: taskGroupLabel(number, agents),
      kind: [...agents].sort((left, right) => urgency(left.kind) - urgency(right.kind))[0]?.kind ?? 'idle',
      agents,
    }))
    .sort((left, right) => urgency(left.kind) - urgency(right.kind) || left.number - right.number);
}

function taskGroupLabel(number, agents) {
  const title = agents.map(agent => agent.taskTitle).find(name => !!name);
  return title ? `#${number} · ${title}` : `#${number}`;
}

function urgency(kind) {
  if (kind === 'waiting') return 0;
  if (kind === 'halted') return 1;
  return kind === 'working' ? 2 : 3;
}

function stateLabel(kind) {
  if (kind === 'waiting') return '◆ gate open';
  if (kind === 'halted') return '▲ halted';
  if (kind === 'working') return '● working';
  return '○ idle';
}

function workspaceRows(workspaces, standings, selectFirst) {
  const kindById = new Map(standings.map(item => [item.id, item.kind]));
  return workspaces.map((workspace, index) => {
    const kind = kindById.get(workspace.id) ?? 'idle';
    const parts = [kind];
    if (workspace.branch) parts.push('branch');
    const state = selectFirst && index === 0 ? ' data-au-state="selected:0"' : '';
    const branch = workspace.branch ? `; branch=${workspace.branch}` : '';
    const props = `name=${workspace.displayName}${branch}; parts=${parts.join(' ')}`;
    return `<div data-au-use="workspace"${state} data-au-props="${esc(props)}"></div>`;
  }).join('\n');
}

function fleetMap(demo) {
  const lanes = demo.lanes.map(lane => `
    <div class="aun-state-map__lane"><div class="aun-state-map__drop"></div><div class="aun-state-map__branch aun-state-map__branch--${lane.kind}">
      <div class="aun-state-map__branch-head">
        <span class="aun-state-map__mark" aria-hidden="true"></span>
        <span class="aun-state-map__branch-copy"><span class="aun-state-map__branch-title">${esc(lane.label)}</span><span class="aun-state-map__branch-meta">${esc(lane.description)}</span></span>
        <span class="aun-state-map__count">${lane.members.length}</span>
      </div>
      ${lane.members.map(agentCard).join('')}
    </div></div>`).join('');
  return lanes;
}

function agentCard(agent) {
  const task = agent.taskNumber == null ? 'No task in hand' : taskGroupLabel(agent.taskNumber, [agent]);
  const messages = agent.messages.map(message => `<p class="aun-state-map__message">${esc(message)}</p>`).join('');
  const field = messages ? `<div class="aun-state-map__messages">${messages}</div>` : '';
  return `<div class="aun-state-map__host"><div class="aun-state-map__agent"><span class="aun-state-map__agent-name">${esc(agent.displayName)}</span><span class="aun-state-map__agent-task">${esc(task)}</span></div>${field}</div>`;
}

function reportedTable(demo) {
  const rows = demo.tasks.map(task => `
    <div class="aun-task-summary__row">
      <span>${esc(task.label)}</span>
      <span>${esc(stateLabel(task.kind))}</span>
      <span>${task.agents.map(agent => esc(agent.role ? `${agent.displayName} · ${agent.role}` : agent.displayName)).join('<br>')}</span>
    </div>`).join('');
  return `
    <p class="aun-rail__section">Tasks</p>
    <div class="aun-task-summary">
      <div class="aun-task-summary__head"><span>Task</span><span>State</span><span>Assigned to</span></div>
      ${rows}
    </div>`;
}

function taskBoard(demo) {
  const sections = [
    ['In progress', demo.working, task => queuedRow(task, demo.names, 'Stop', 'aun-btn aun-btn--danger', false)],
    ['Queue', demo.waiting, task => queuedRow(task, demo.names, 'Dequeue', 'aun-btn aun-btn--danger', true)],
    ['Open issues', demo.offered, issue => `
      <div class="aun-task-row">
        <span class="aun-task-row__body"><span class="aun-task-row__title">${esc(`#${issue.number} · ${issue.title}`)}</span></span>
        <button class="aun-btn aun-btn--primary" type="button">Queue</button>
      </div>`],
  ];
  const groups = sections.filter(([, items]) => items.length).map(([label, items, row]) => `
    <div class="aun-group">
      <div data-au-use="section-label" data-au-props="label=${esc(`${label} · ${items.length}`)}"></div>
      ${items.map(row).join('')}
    </div>`).join('');
  return groups;
}

function queuedRow(task, names, action, buttonClass, grip) {
  const holder = task.heldByWorkspaceId ? names.get(task.heldByWorkspaceId) : null;
  const who = holder ? `<span class="aun-task-row__agent">${esc(holder)}</span>` : '';
  const handle = grip ? '<span class="aun-task__grip" aria-hidden="true"><span></span><span></span><span></span></span>' : '';
  return `
    <div class="aun-task-row">
      ${handle}
      <span class="aun-task-row__body">
        <span class="aun-task-row__title">${esc(`#${task.number} · ${task.title}`)}</span>
        ${who}
        <span class="aun-task-row__repo">${esc(`${task.owner}/${task.repository}`)}</span>
      </span>
      <button class="${buttonClass}" type="button">${esc(action)}</button>
    </div>`;
}

function taskSource(demo) {
  const label = demo.source ? `${demo.source.owner}/${demo.source.repository}` : 'Not connected';
  return `<p class="aun-rail__section">Task source</p><strong class="au-workspace-name">${esc(label)}</strong>`;
}

function decisions(demo) {
  const rows = demo.decisions.map(decision => `<p class="aun-rail__agent"><strong>${esc(decision.headline)}</strong><span>${esc(decision.agent)}</span></p>`).join('');
  return `<p class="aun-rail__section">Train</p>${rows}<button type="button" class="aun-btn">Export</button>`;
}

function memberOverview(demo) {
  const workspace = demo.open;
  if (!workspace) return '';
  const stats = demo.overview[workspace.id] ?? {};
  const applications = workspace.applications?.length ?? 0;
  return `
    <div class="au-overview">
      <div><h1 class="au-page-title">${esc(workspace.displayName)}</h1><p class="au-muted">${esc(workspace.branch)}</p></div>
      <div class="au-overview-grid">
        <div class="au-card"><span class="au-field-label">State</span><strong class="au-accent">${esc(workspace.state)}</strong></div>
        <div class="au-card"><span class="au-field-label">CPU</span><strong>${esc(percent(stats.cpuPercent))}</strong></div>
        <div class="au-card"><span class="au-field-label">Memory</span><strong>${esc(bytes(stats.memoryBytes))}</strong></div>
        <div class="au-card"><span class="au-field-label">Storage</span><strong>${esc(bytes(stats.storageBytes))}</strong></div>
        <div class="au-card"><span class="au-field-label">Processes</span><strong>${esc(stats.processCount ?? '')}</strong></div>
        <div class="au-card"><span class="au-field-label">Applications</span><strong>${applications}</strong></div>
      </div>
      <div class="au-stack">
        <div><span class="au-field-label">Repository</span><p class="au-mono">${esc(workspace.repositoryPath)}</p></div>
        <div><span class="au-field-label">Worktree</span><p class="au-mono">${esc(workspace.worktreePath)}</p></div>
        <div><span class="au-field-label">Commit</span><p class="au-mono">${esc(workspace.commit)}</p></div>
      </div>
    </div>`;
}

const showcaseStatus = ['running', 'failed', 'pending'];
const ledTone = { passed: 'healthy', running: 'warning', failed: 'danger', pending: 'idle' };

function validationFor(definition, workspace) {
  const application = workspace?.applications?.[0]?.name;
  if (!workspace || !application) return [];
  return (definition.validationFlows ?? [])
    .filter(flow => flow.workspaceId === workspace.id && flow.application === application)
    .map((flow, index) => presentFlow(flow, showcaseStatus[index] ?? 'pending'));
}

function presentFlow(flow, status) {
  const stages = paintStages(stagesOf(flow), status);
  return { name: flow.name, description: flow.description ?? '', status, stages };
}

function stagesOf(flow) {
  const stages = [];
  if (flow.initialExpectations?.length) {
    stages.push({ label: 'Starting page', checks: flow.initialExpectations.map(checkOf) });
  }
  for (const step of flow.steps ?? []) {
    stages.push({ label: step.description || step.id, checks: (step.expectations ?? []).map(checkOf) });
  }
  return stages;
}

function checkOf(expectation) {
  const value = expectation.value ?? '';
  const label = expectation.kind === 'Title' ? `Title is ${value}` : value || expectation.kind || '';
  return { label, status: 'pending' };
}

function paintStages(stages, status) {
  return stages.map((stage, index) => {
    const stageStatus = status === 'passed' ? 'passed'
      : status === 'pending' ? 'pending'
      : status === 'failed' ? (index === 0 ? 'failed' : 'pending')
      : index < 2 ? 'passed' : index === 2 ? 'running' : 'pending';
    return { label: stage.label, status: stageStatus, checks: paintChecks(stage.checks, stageStatus) };
  });
}

function paintChecks(checks, stageStatus) {
  return checks.map((check, index) => ({
    label: check.label,
    status: stageStatus === 'passed' ? 'passed'
      : stageStatus === 'pending' ? 'pending'
      : stageStatus === 'failed' ? (index === 0 ? 'failed' : 'pending')
      : index === 0 ? 'running' : 'pending',
  }));
}

function visibleStage(flow) {
  if (flow.status === 'passed') return flow.stages.at(-1) ?? null;
  return flow.stages.find(stage => stage.status === 'running')
    ?? flow.stages.find(stage => stage.status === 'failed')
    ?? flow.stages.find(stage => stage.status === 'pending')
    ?? flow.stages.at(-1)
    ?? null;
}

function led(status) {
  return `<span class="au-status-dot au-status-dot--${ledTone[status] ?? 'idle'}" aria-hidden="true"></span>`;
}

function flowCard(flow, withPlay) {
  const play = withPlay ? '<button class="au-validation-play" type="button">▶ Play</button>' : '';
  const stages = flow.stages.map(stage => `
    <div class="au-validation-stage">
      <div class="au-validation-step">${led(stage.status)}<span>${esc(stage.label)}</span></div>
      ${stage.checks.map(check => `<div class="au-validation-check">${led(check.status)}<span>${esc(check.label)}</span></div>`).join('')}
    </div>`).join('');
  return `
    <div class="au-validation-flow">
      <div class="au-validation-flow-head">${led(flow.status)}<div class="au-validation-copy"><strong>${esc(flow.name)}</strong><p class="au-muted">${esc(flow.description)}</p></div>${play}</div>
      ${stages}
    </div>`;
}

function validationRail(flows) {
  return `
    <div class="au-validation-sidebar-head">
      <div class="au-validation-sidebar-copy">
        <p class="au-accent">Validation</p>
        <p class="au-muted">Checks for the displayed app</p>
      </div>
      <button class="au-workspace-add" type="button" aria-label="Validation">›</button>
    </div>
    <div class="au-validation-sidebar-list">${flows.map(flow => flowCard(flow, true)).join('')}</div>`;
}

function validationDock(flows) {
  const chosen = flows[1] ?? flows[0] ?? null;
  if (!chosen) return '';
  const checks = chosen.stages.flatMap(stage => stage.checks);
  const frames = [
    { kind: 'idle' },
    { kind: 'idle' },
    { kind: 'menu' },
    { kind: 'menu', current: 1 },
    { kind: 'selected' },
    { kind: 'selected' },
    ...checks.flatMap(check => [
      { kind: 'check', check, status: 'running' },
      { kind: 'check', check, status: 'passed' },
    ]),
    { kind: 'overview', check: checks.at(-1), status: 'passed' },
    { kind: 'overview', check: checks.at(-1), status: 'passed' },
    { kind: 'overview', check: checks.at(-1), status: 'passed' },
  ];
  const count = frames.length;
  const slice = 100 / count;
  const showUntil = (slice - 0.02).toFixed(4);
  const beat = (index, extra = '') => ` class="aun-beat${extra}" style="--aun-i:${index}"`;
  const menu = current => `<div class="aun-validate__menu" role="listbox" aria-label="Validations">${flows.map((flow, index) => {
    const marked = index === current ? ' aria-current="true"' : '';
    return `<button class="aun-validate__item" type="button" role="option"${marked}>${led('pending')}<span>${esc(flow.name)}</span></button>`;
  }).join('')}</div>`;
  const sheet = `
    <div class="aun-validate__sheet" role="dialog" aria-label="${esc(chosen.name)}">
      <div class="aun-validate__sheet-title">${led('passed')}<span>${esc(chosen.name)}</span></div>
      ${chosen.stages.map(stage => `
        <div class="au-validation-stage">
          <div class="au-validation-step">${led('passed')}<span>${esc(stage.label)}</span></div>
          ${stage.checks.map(check => `<div class="au-validation-check">${led('passed')}<span>${esc(check.label)}</span></div>`).join('')}
        </div>`).join('')}
    </div>`;
  const line = frame => {
    if (frame.kind === 'idle' || frame.kind === 'menu') return '<p class="au-muted">No check selected</p>';
    const titleStatus = frame.kind === 'overview' ? 'passed' : frame.kind === 'selected' ? 'pending' : 'running';
    const sub = frame.check
      ? `<p class="aun-validate__step">${led(frame.status)}<span>${esc(frame.check.label)}</span></p>`
      : '';
    return `<strong class="aun-validate__title">${led(titleStatus)}<span>${esc(chosen.name)}</span></strong>${sub}`;
  };
  const overlays = frames.map((frame, index) => {
    if (frame.kind === 'menu') return `<div${beat(index, ' aun-validate__overlay')}>${menu(frame.current)}</div>`;
    if (frame.kind === 'overview') return `<div${beat(index, ' aun-validate__overlay')}>${sheet}</div>`;
    return '';
  }).join('');
  const lines = frames.map((frame, index) => `<div${beat(index)}>${line(frame)}</div>`).join('');
  const chevrons = frames.map((frame, index) => {
    const open = frame.kind === 'menu';
    return `<button${beat(index)} type="button" aria-label="Validations" aria-expanded="${open ? 'true' : 'false'}">${open ? '▴' : '▾'}</button>`;
  }).join('');
  return `
    <div class="aun-validate__player" style="--aun-n:${count};--aun-run:${(count * 0.85).toFixed(2)}s">
      <style>@keyframes aun-validate-beat{0%,${showUntil}%{opacity:1;visibility:visible}${slice.toFixed(4)}%,100%{opacity:0;visibility:hidden}}</style>
      ${overlays}
      <div class="au-validation-dock">
        <button class="au-validation-play aun-validate__run" type="button" aria-label="Run">▶</button>
        <div class="au-validation-dock-copy aun-validate__copy">${lines}</div>
        <div class="aun-validate__chevron">${chevrons}</div>
      </div>
    </div>`;
}

function memberHead(workspace) {
  if (!workspace) return '';
  return `<div data-au-use="page-title" data-au-props="title=${esc(workspace.displayName)}"></div><span class="aun-phone__subtitle">${esc(workspace.branch)}</span>`;
}

function percent(value) {
  return value == null ? '' : `${value}%`;
}

function bytes(value) {
  if (value == null) return '';
  const megabytes = value / (1024 * 1024);
  if (megabytes < 1024) return `${Math.round(megabytes)} MB`;
  return `${(megabytes / 1024).toFixed(1)} GB`;
}

function plural(count, word) {
  return count === 1 ? word : `${word}s`;
}

function same(left, right) {
  return (left ?? '').toLowerCase() === right.toLowerCase();
}

function fillSlot(html, name, inner) {
  const marker = `data-au-demo="${name}"`;
  const at = html.indexOf(marker);
  if (at < 0) return html;
  const start = html.lastIndexOf('<', at);
  const open = html.indexOf('>', at);
  const tag = html.slice(start + 1).match(/^([a-zA-Z][\w:-]*)/);
  if (!tag || open < 0) return html;
  const closeAt = closeOf(html, tag[1], open + 1);
  if (closeAt < 0) return html;
  return `${html.slice(0, open + 1)}${inner}${html.slice(closeAt)}`;
}

function setText(html, name, value) {
  const marker = `data-au-text="${name}"`;
  const at = html.indexOf(marker);
  if (at < 0) return null;
  const open = html.indexOf('>', at);
  const close = html.indexOf('<', open + 1);
  if (open < 0 || close < 0) return null;
  return `${html.slice(0, open + 1)}${value}${html.slice(close)}`;
}

function closeOf(html, tag, from) {
  const open = `<${tag}`;
  const close = `</${tag}>`;
  let depth = 1;
  let cursor = from;
  while (cursor < html.length && depth > 0) {
    const nextClose = html.indexOf(close, cursor);
    if (nextClose < 0) return -1;
    const nextOpen = html.indexOf(open, cursor);
    if (nextOpen >= 0 && nextOpen < nextClose) {
      depth += 1;
      cursor = nextOpen + open.length;
      continue;
    }
    depth -= 1;
    if (depth === 0) return nextClose;
    cursor = nextClose + close.length;
  }
  return -1;
}

function esc(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}
