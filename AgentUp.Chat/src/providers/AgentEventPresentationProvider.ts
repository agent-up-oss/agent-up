import type {
  AgentActivity,
  AgentActivityKind,
  AgentPermission,
  AgentPermissionOption,
  AgentSession,
  SessionContext,
  TranscriptItem,
} from '../models/AgentSession';
import { durationBetween } from './ToolCallProvider';

export type PresentedUpdate =
  | { kind: 'message'; role: 'agent' | 'thought'; text: string }
  | {
    kind: 'tool'; toolCallId: string; title?: string; status?: string; locations?: string[]; detail?: string; toolKind?: string; rawInput?: string;
    rawOutput?: string; unifiedDiff?: string; diffPath?: string; diffOldText?: string; diffNewText?: string;
  }
  | { kind: 'plan'; entries: { content: string; status: string }[] }
  | { kind: 'context'; context: Partial<SessionContext> }
  | { kind: 'ignore' };

export function unwrapSessionUpdate(payload: unknown): unknown {
  return record(payload)?.update ?? payload;
}

export function mergeAgentSession(previous: AgentSession | null, next: AgentSession | null): AgentSession | null {
  if (!next) {
    if (!previous) return null;
    return { ...previous, agent: null, sessionId: null, state: 'idle', error: null };
  }

  const boundAgent = next.boundAgent ?? previous?.boundAgent;
  const merged = next.agents.length > 0
    ? { ...next, boundAgent }
    : previous?.agents.length
      ? { ...next, agents: previous.agents, boundAgent }
      : { ...next, boundAgent };
  return keepAuthenticatingChallenge(previous, merged);
}

/**
 * A GET started before the login CLI printed its link must not wipe a challenge the event
 * stream already delivered. Keep the URL only while both snapshots are still `authenticating`.
 */
function keepAuthenticatingChallenge(previous: AgentSession | null, next: AgentSession): AgentSession {
  if (next.state !== 'authenticating' || previous?.state !== 'authenticating' || !previous.loginChallenge?.url) return next;
  if (!next.loginChallenge?.url) return { ...next, loginChallenge: previous.loginChallenge };
  if (next.loginChallenge.url !== previous.loginChallenge.url) return next;

  // Authentication updates arrive through both GET and the event stream. A GET started when the
  // CLI had printed only its URL can finish after the event containing its device code. Merge
  // same-challenge fields monotonically so that stale response cannot put the UI back into the
  // incomplete (and historically wrong pasted-code) state.
  return {
    ...next,
    loginChallenge: {
      ...next.loginChallenge,
      code: next.loginChallenge.code ?? previous.loginChallenge.code,
      instructions: next.loginChallenge.code
        ? next.loginChallenge.instructions
        : previous.loginChallenge.code
          ? previous.loginChallenge.instructions
          : next.loginChallenge.instructions ?? previous.loginChallenge.instructions,
      canSubmitCode: next.loginChallenge.canSubmitCode === true || previous.loginChallenge.canSubmitCode === true,
      expiresAt: next.loginChallenge.expiresAt ?? previous.loginChallenge.expiresAt,
      redirectUri: next.loginChallenge.redirectUri ?? previous.loginChallenge.redirectUri,
    },
  };
}

export function sessionUpdateKind(update: unknown): string {
  return stringField(update, 'sessionUpdate') ?? stringField(update, 'type') ?? '';
}

export function agentEventRole(update: unknown): 'agent' | 'thought' | 'tool' | 'system' {
  const kind = sessionUpdateKind(update);
  if (kind.includes('thought')) return 'thought';
  if (kind.includes('tool')) return 'tool';
  if (kind.includes('message') && !kind.includes('user_message')) return 'agent';
  return 'system';
}

export function agentEventText(value: unknown): string {
  return contentText(value);
}

/** Caption on a message sent while a turn runs: it steers the next turn, it does not stop this one. */
export const steeringStatus = 'Steering · next turn';

/** The transcript row for a Server `user_message` event, marking steering sent mid-turn. */
export function userMessageItem(sequence: number, payload: unknown, timestamp?: string): TranscriptItem {
  const steering = typeof payload === 'object' && payload !== null && (payload as { steering?: unknown }).steering === true;
  return { id: `${sequence}`, role: 'user', text: agentEventText(payload), ...(steering ? { status: steeringStatus } : {}), ...(timestamp ? { startedAt: timestamp } : {}) };
}

export function presentSessionUpdate(update: unknown): PresentedUpdate {
  const kind = sessionUpdateKind(update);
  if (!kind || kind.includes('user_message') || kind.includes('available_commands') || kind.includes('config_option')) {
    return { kind: 'ignore' };
  }
  if (kind.includes('thought')) {
    const text = contentText(update);
    return text ? { kind: 'message', role: 'thought', text } : { kind: 'ignore' };
  }
  if (kind.includes('tool')) {
    return presentTool(update);
  }
  if (kind.includes('plan')) {
    const entries = planEntries(update);
    return entries.length ? { kind: 'plan', entries } : { kind: 'ignore' };
  }
  if (kind.includes('message')) {
    const text = contentText(update);
    return text ? { kind: 'message', role: 'agent', text } : { kind: 'ignore' };
  }
  if (kind.includes('session_info') || kind === 'title') {
    const title = stringField(update, 'title');
    return title ? { kind: 'context', context: { title } } : { kind: 'ignore' };
  }
  if (kind.includes('mode')) {
    const mode = stringField(update, 'currentModeId') ?? stringField(update, 'mode');
    return mode ? { kind: 'context', context: { mode } } : { kind: 'ignore' };
  }
  if (kind.includes('usage')) {
    const usage = formatUsage(update);
    return usage ? { kind: 'context', context: { usage } } : { kind: 'ignore' };
  }
  if (kind.includes('compaction')) {
    return { kind: 'context', context: { compacting: !kind.includes('summary') } };
  }
  return { kind: 'ignore' };
}

export function runSummary(items: TranscriptItem[], duration?: string): string {
  const tools = items.filter(item => item.role === 'tool').length;
  const parts = [duration ? `Worked for ${duration}` : 'Worked'];
  if (tools === 1) parts.push('1 tool');
  else if (tools > 1) parts.push(`${tools} tools`);
  if (items.some(item => item.role === 'thought')) parts.push('Thought');
  return parts.join(' · ');
}

export type TranscriptBlock =
  | { type: 'user'; item: TranscriptItem }
  | { type: 'run'; id: string; items: TranscriptItem[]; duration?: string }
  | { type: 'reply'; item: TranscriptItem };

export function liveRunId(blocks: TranscriptBlock[]): string | undefined {
  for (let i = blocks.length - 1; i >= 0; i -= 1) {
    const block = blocks[i]!;
    if (block.type === 'user') return undefined;
    if (block.type === 'run') return block.id;
  }
  return undefined;
}

export function groupTranscript(items: TranscriptItem[]): TranscriptBlock[] {
  const blocks: TranscriptBlock[] = [];
  let run: TranscriptItem[] = [];
  let promptedAt: string | undefined;
  const flush = () => {
    if (run.length === 0) return;
    let result: TranscriptItem | undefined;
    let work = run;
    for (let i = run.length - 1; i >= 0; i -= 1) {
      if (run[i]!.role !== 'agent') continue;
      result = run[i];
      work = run.filter((_, index) => index !== i);
      break;
    }
    // A turn's time runs from the prompt, so the model's thinking before its first step counts.
    const startedAt = promptedAt ?? run[0]!.startedAt;
    const endedAt = run.map(item => item.endedAt).filter((value): value is string => Boolean(value)).sort().at(-1);
    const duration = durationBetween(startedAt, endedAt);
    if (work.length > 0) blocks.push({ type: 'run', id: work[0]!.id, items: work, ...(duration ? { duration } : {}) });
    if (result) blocks.push({ type: 'reply', item: result });
    run = [];
  };
  for (const item of items) {
    if (item.role === 'user') {
      flush();
      if (!item.status) promptedAt = item.startedAt;
      blocks.push({ type: 'user', item });
      continue;
    }
    run.push(item);
    if (item.turnEnd) {
      // Steering queued during this turn runs next with no prompt of its own, so it starts its
      // own run and reply, timed from when this turn ended.
      const endedAt = item.endedAt;
      flush();
      promptedAt = endedAt;
    }
  }
  flush();
  return blocks;
}

/** A run's work rows: consecutive finished reads, or searches, fold into one "Read 3 files" row. */
export type WorkEntry =
  | { type: 'item'; item: TranscriptItem }
  | { type: 'group'; id: string; kind: 'read' | 'search'; title: string; summary: string; items: TranscriptItem[] };

export function groupWork(items: TranscriptItem[], summaryOf: (item: TranscriptItem) => string | undefined): WorkEntry[] {
  const entries: WorkEntry[] = [];
  let index = 0;
  while (index < items.length) {
    const kind = groupable(items[index]!);
    let end = index;
    while (kind && end + 1 < items.length && groupable(items[end + 1]!) === kind) end++;
    if (kind && end > index) {
      const group = items.slice(index, end + 1);
      entries.push({
        type: 'group',
        id: group[0]!.toolCallId ?? group[0]!.id,
        kind,
        title: kind === 'read' ? `Read ${group.length} files` : `Searched ${group.length} times`,
        summary: [...new Set(group.map(summaryOf).filter((value): value is string => Boolean(value)))].join(', '),
        items: group,
      });
    } else {
      entries.push({ type: 'item', item: items[index]! });
    }
    index = end + 1;
  }
  return entries;
}

function groupable(item: TranscriptItem): 'read' | 'search' | undefined {
  return item.role === 'tool' && item.status === 'completed' && (item.toolKind === 'read' || item.toolKind === 'search') ? item.toolKind : undefined;
}

/** "Thinking" while it streams, then "Thought for 4s" once it took long enough to say. */
export function thoughtLabel(item: TranscriptItem, live: boolean): string {
  if (live) return 'Thinking';
  const duration = durationBetween(item.startedAt, item.endedAt);
  return duration ? `Thought for ${duration}` : 'Thought';
}

/** Marks the turn that just ended, so the next turn's text starts a new run and reply. */
export function endTurn(items: TranscriptItem[], at?: string): TranscriptItem[] {
  const last = items.at(-1);
  if (!last || last.role === 'user' || last.turnEnd) return items;
  return [...items.slice(0, -1), { ...last, turnEnd: true, endedAt: last.endedAt ?? at }];
}

export function applyPresentedUpdate(items: TranscriptItem[], id: string, presented: PresentedUpdate, at?: string): TranscriptItem[] {
  if (presented.kind === 'message') {
    const last = items.at(-1);
    if (last?.role === presented.role && !last.turnEnd) return [...items.slice(0, -1), { ...last, text: last.text + presented.text, ...(at ? { endedAt: at } : {}) }];
    return [...items, { id, role: presented.role, text: presented.text, ...(at ? { startedAt: at, endedAt: at } : {}) }];
  }
  if (presented.kind === 'tool') {
    const existing = items.findIndex(entry => entry.role === 'tool' && entry.toolCallId === presented.toolCallId);
    const previous = existing >= 0 ? items[existing] : undefined;
    const title = presented.title ?? previous?.title ?? 'Tool';
    const status = presented.status ?? previous?.status ?? 'pending';
    const locations = presented.locations ?? previous?.locations ?? [];
    const body = presented.detail ?? toolBody(previous);
    const detail = [locations.join(', '), body].filter(Boolean).join('\n');
    const toolKind = presented.toolKind ?? previous?.toolKind;
    const rawInput = presented.rawInput ?? previous?.rawInput;
    const output = presented.detail ?? previous?.output;
    const name = previous?.name ?? presented.title;
    const startedAt = previous?.startedAt ?? at;
    const finished = status === 'completed' || status === 'failed';
    const endedAt = finished ? previous?.endedAt && previous.status === status ? previous.endedAt : at : undefined;
    const carried = {
      rawOutput: presented.rawOutput ?? previous?.rawOutput,
      unifiedDiff: presented.unifiedDiff ?? previous?.unifiedDiff,
      diffPath: presented.diffPath ?? previous?.diffPath,
      diffOldText: presented.diffOldText ?? previous?.diffOldText,
      diffNewText: presented.diffNewText ?? previous?.diffNewText,
    };
    const item: TranscriptItem = {
      id: previous?.id ?? id,
      role: 'tool',
      toolCallId: presented.toolCallId,
      title,
      status,
      locations,
      text: detail ? `${title}\n${detail}` : title,
      ...(toolKind ? { toolKind } : {}),
      ...(rawInput ? { rawInput } : {}),
      ...(output ? { output } : {}),
      ...(name ? { name } : {}),
      ...Object.fromEntries(Object.entries(carried).filter(([, value]) => value !== undefined)),
      ...(startedAt ? { startedAt } : {}),
      ...(endedAt ? { endedAt } : {}),
    };
    if (existing < 0) return [...items, item];
    const next = [...items];
    next[existing] = item;
    return next;
  }
  if (presented.kind === 'plan') {
    const item: TranscriptItem = { ...(at ? { startedAt: at, endedAt: at } : {}), id, role: 'plan', text: presented.entries.map(entry => `${statusMark(entry.status)} ${entry.content}`).join('\n'), entries: presented.entries };
    const existing = items.findIndex(entry => entry.role === 'plan');
    if (existing < 0) return [...items, item];
    const next = [...items];
    next[existing] = { ...item, id: items[existing].id };
    return next;
  }
  return items;
}

export function mergeContext(current: SessionContext, patch: Partial<SessionContext>): SessionContext {
  const next = { ...current };
  if (patch.title !== undefined) next.title = patch.title ?? undefined;
  if (patch.mode !== undefined) next.mode = patch.mode;
  if (patch.usage !== undefined) next.usage = patch.usage;
  if (patch.compacting !== undefined) next.compacting = patch.compacting;
  return next;
}

export function activityHint(presented: PresentedUpdate): { kind: AgentActivityKind; toolTitle?: string } | null {
  if (presented.kind === 'message') return { kind: presented.role === 'thought' ? 'thinking' : 'writing' };
  if (presented.kind === 'tool') return { kind: 'tool', toolTitle: presented.title };
  if (presented.kind === 'plan') return { kind: 'plan' };
  if (presented.kind === 'context' && presented.context.compacting) return { kind: 'compacting' };
  return null;
}

export function resolveActivity(input: {
  state?: string | null;
  error?: string | null;
  hasPermission: boolean;
  hint?: { kind: AgentActivityKind; toolTitle?: string } | null;
}): AgentActivity {
  if (input.state === 'authentication_required' || input.state === 'authenticating') return { kind: 'auth', label: 'Waiting for sign-in' };
  if (input.error) return { kind: 'error', label: input.error };
  if (input.hasPermission) return { kind: 'permission', label: 'Waiting for a decision' };
  if (input.state === 'stopped') return { kind: 'stopped', label: 'Stopped' };
  if (input.state === 'running') {
    if (input.hint?.kind === 'thinking') return { kind: 'thinking', label: 'Thinking' };
    if (input.hint?.kind === 'tool') return { kind: 'tool', label: input.hint.toolTitle ? `Using ${input.hint.toolTitle}` : 'Using a tool' };
    if (input.hint?.kind === 'writing') return { kind: 'writing', label: 'Writing' };
    if (input.hint?.kind === 'plan') return { kind: 'plan', label: 'Working through the plan' };
    if (input.hint?.kind === 'compacting') return { kind: 'compacting', label: 'Compacting context' };
    return { kind: 'running', label: 'Working' };
  }
  if (input.state === 'ready') return { kind: 'ready', label: 'Idle' };
  if (input.state === 'idle') return { kind: 'idle', label: 'Idle' };
  return { kind: 'idle', label: input.state ?? 'Loading' };
}

/**
 * The open prompt after a `permission_decision` event: closed when the Server decided that
 * request, whichever client answered it, and left alone for any other request.
 */
export function afterPermissionDecision(open: AgentPermission | null, payload: unknown): AgentPermission | null {
  const requestId = record(payload)?.requestId;
  return open && typeof requestId === 'string' && requestId === open.requestId ? null : open;
}

export function parsePermission(payload: unknown): AgentPermission | null {
  const outer = record(payload);
  if (!outer) return null;
  const requestId = typeof outer.requestId === 'string' ? outer.requestId : '';
  if (!requestId) return null;
  const request = record(outer.request) ?? outer;
  const options = permissionOptions(request.options);
  if (!options.length) return null;
  const toolCall = record(request.toolCall) ?? record(record(request.subject)?.toolCall);
  const command = record(request.subject)?.type === 'command' ? record(request.subject) : null;
  const locations = locationsOf(toolCall).concat(typeof command?.cwd === 'string' ? [command.cwd] : []);
  const title = stringField(request, 'title')
    ?? stringField(toolCall, 'title')
    ?? (typeof command?.command === 'string' ? command.command : null)
    ?? 'Agent requests permission';
  const detail = stringField(request, 'description') ?? (typeof command?.command === 'string' ? command.command : undefined);
  return {
    requestId,
    title,
    detail: detail && detail !== title ? detail : undefined,
    toolKind: stringField(toolCall, 'kind') ?? undefined,
    locations,
    options,
  };
}

export function permissionOptionLabel(option: AgentPermissionOption): string {
  if (option.name?.trim()) return option.name;
  switch (option.kind) {
    case 'allow_once': return 'Allow once';
    case 'allow_always': return 'Always allow';
    case 'reject_once': return 'Reject';
    case 'reject_always': return 'Always reject';
    default: return option.optionId;
  }
}

export function permissionOptionTone(kind: string | undefined): 'allow' | 'reject' | 'neutral' {
  if (kind?.startsWith('allow')) return 'allow';
  if (kind?.startsWith('reject')) return 'reject';
  return 'neutral';
}

export function visibleText(text: string): string {
  return text.replace(/\*\*(.*?)\*\*/g, '$1').replace(/`([^`]+)`/g, '$1');
}

function presentTool(update: unknown): PresentedUpdate {
  const toolCallId = stringField(update, 'toolCallId') ?? 'tool';
  const rec = record(update);
  return {
    kind: 'tool',
    toolCallId,
    title: stringField(update, 'title') ?? undefined,
    status: stringField(update, 'status') ?? undefined,
    locations: rec && 'locations' in rec ? locationsOf(update) : undefined,
    detail: rec && 'content' in rec ? contentText(rec.content) : undefined,
    toolKind: stringField(update, 'kind') ?? undefined,
    rawInput: rawInputOf(rec),
    ...toolOutputOf(rec),
  };
}

/**
 * What a finished call returned beyond its visible content: the raw output text (a read's file
 * text lives there), a unified diff from the tool's metadata, and ACP's diff content item.
 */
function toolOutputOf(rec: Record<string, unknown> | null | undefined) {
  const raw = rec?.rawOutput;
  const rawRecord = record(raw);
  const rawOutput = typeof raw === 'string' ? raw : typeof rawRecord?.output === 'string' ? rawRecord.output : undefined;
  const metadataDiff = record(rawRecord?.metadata)?.diff;
  const diffItem = Array.isArray(rec?.content) ? rec.content.map(record).find(entry => entry?.type === 'diff') : undefined;
  const values = {
    rawOutput: rawOutput?.trim() ? rawOutput : undefined,
    unifiedDiff: typeof metadataDiff === 'string' && metadataDiff.trim() ? metadataDiff : undefined,
    diffPath: stringField(diffItem, 'path') ?? undefined,
    diffOldText: typeof diffItem?.oldText === 'string' ? diffItem.oldText : undefined,
    diffNewText: typeof diffItem?.newText === 'string' ? diffItem.newText : undefined,
  };
  return Object.fromEntries(Object.entries(values).filter(([, value]) => value !== undefined)) as Partial<typeof values>;
}

function rawInputOf(rec: Record<string, unknown> | null | undefined): string | undefined {
  const input = rec?.rawInput;
  return input && typeof input === 'object' && !Array.isArray(input) && Object.keys(input).length ? JSON.stringify(input) : undefined;
}

function toolBody(item: TranscriptItem | undefined): string {
  if (!item?.text) return '';
  const lines = item.text.split('\n').slice(1);
  if (!lines.length) return '';
  if (item.locations?.length && lines[0] === item.locations.join(', ')) return lines.slice(1).join('\n');
  return lines.join('\n');
}

function planEntries(update: unknown): { content: string; status: string }[] {
  const entries = record(update)?.entries;
  if (!Array.isArray(entries)) return [];
  return entries.flatMap(entry => {
    const content = stringField(entry, 'content') ?? contentText(entry);
    return content ? [{ content, status: stringField(entry, 'status') ?? 'pending' }] : [];
  });
}

function permissionOptions(value: unknown): AgentPermissionOption[] {
  if (!Array.isArray(value)) return [];
  return value.flatMap(entry => {
    const optionId = stringField(entry, 'optionId');
    if (!optionId) return [];
    return [{ optionId, name: stringField(entry, 'name') ?? '', kind: stringField(entry, 'kind') ?? undefined }];
  });
}

function locationsOf(value: unknown): string[] {
  const locations = record(value)?.locations;
  if (!Array.isArray(locations)) return [];
  return locations.flatMap(entry => {
    const path = stringField(entry, 'path');
    return path ? [path] : [];
  });
}

function formatUsage(update: unknown): string | null {
  const used = numberField(update, 'used');
  const size = numberField(update, 'size');
  if (used == null || size == null) return null;
  return `${formatTokens(used)} / ${formatTokens(size)}`;
}

function formatTokens(value: number): string {
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 10_000) return `${Math.round(value / 1000)}k`;
  if (value >= 1000) return `${(value / 1000).toFixed(1)}k`;
  return String(value);
}

function contentText(value: unknown): string {
  if (typeof value === 'string') return value;
  if (Array.isArray(value)) return value.map(contentText).filter(Boolean).join('\n');
  const rec = record(value);
  if (!rec) return '';
  return contentText(rec.text ?? rec.content ?? rec.message);
}

function statusMark(status: string): string {
  if (status === 'completed') return '✓';
  if (status === 'in_progress') return '●';
  return '○';
}

function stringField(value: unknown, key: string): string | null {
  const found = record(value)?.[key];
  return typeof found === 'string' && found.trim() ? found : null;
}

function numberField(value: unknown, key: string): number | null {
  const found = record(value)?.[key];
  return typeof found === 'number' && Number.isFinite(found) ? found : null;
}

function record(value: unknown): Record<string, unknown> | null {
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null;
}
