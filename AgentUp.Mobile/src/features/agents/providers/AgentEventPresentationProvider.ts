import type {
  AgentActivity,
  AgentActivityKind,
  AgentPermission,
  AgentPermissionOption,
  SessionContext,
  TranscriptItem,
} from '../models/AgentSession';

export type PresentedUpdate =
  | { kind: 'message'; role: 'agent' | 'thought'; text: string }
  | { kind: 'tool'; toolCallId: string; title?: string; status?: string; locations?: string[]; detail?: string }
  | { kind: 'plan'; entries: { content: string; status: string }[] }
  | { kind: 'context'; context: Partial<SessionContext> }
  | { kind: 'ignore' };

export function unwrapSessionUpdate(payload: unknown): unknown {
  return record(payload)?.update ?? payload;
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

export function applyPresentedUpdate(items: TranscriptItem[], id: string, presented: PresentedUpdate): TranscriptItem[] {
  if (presented.kind === 'message') {
    const last = items.at(-1);
    if (last?.role === presented.role) return [...items.slice(0, -1), { ...last, text: last.text + presented.text }];
    return [...items, { id, role: presented.role, text: presented.text }];
  }
  if (presented.kind === 'tool') {
    const existing = items.findIndex(entry => entry.role === 'tool' && entry.toolCallId === presented.toolCallId);
    const previous = existing >= 0 ? items[existing] : undefined;
    const title = presented.title ?? previous?.title ?? 'Tool';
    const status = presented.status ?? previous?.status ?? 'pending';
    const locations = presented.locations ?? previous?.locations ?? [];
    const body = presented.detail ?? toolBody(previous);
    const detail = [locations.join(', '), body].filter(Boolean).join('\n');
    const item: TranscriptItem = {
      id: previous?.id ?? id,
      role: 'tool',
      toolCallId: presented.toolCallId,
      title,
      status,
      locations,
      text: detail ? `${title}\n${detail}` : title,
    };
    if (existing < 0) return [...items, item];
    const next = [...items];
    next[existing] = item;
    return next;
  }
  if (presented.kind === 'plan') {
    const item: TranscriptItem = { id, role: 'plan', text: presented.entries.map(entry => `${statusMark(entry.status)} ${entry.content}`).join('\n'), entries: presented.entries };
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
  if (input.error) return { kind: 'error', label: input.error };
  if (input.state === 'authentication_required') return { kind: 'auth', label: 'Waiting for sign-in' };
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
  };
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
