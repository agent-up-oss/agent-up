/**
 * How a tool call reads in the transcript, derived from what the agent reported. It mirrors
 * Desktop's AgentToolCallProvider: an MCP tool named `server_tool` reads as its action with the
 * server as the source (Agent-Up for `agent-up-*`), the agent's own tools are named by their
 * ACP kind, the summary is the one argument that says what the call was about, and output is
 * shown indented when it is JSON and folded behind a count when it is long.
 */
import { diffFromTexts, diffFromUnified, diffNewFile, type Diff } from './DiffProvider';

export type ToolCallState = 'running' | 'done' | 'failed';
export type ToolCallView = {
  title: string;
  source?: string;
  summary?: string;
  state: ToolCallState;
  input?: string;
  output?: string;
  fullOutput?: string;
  hiddenLines: number;
  /** What an edit or a new file changed, when the agent said. */
  diff?: Diff;
  /** How long the call took, once that was long enough to be worth saying. */
  duration?: string;
};

/**
 * Everything the agent reported about one call, merged across its updates. `name` is the first
 * title, which is the tool's name; the completed update often retitles the call with a path.
 */
export type ToolCallFacts = {
  name?: string;
  title?: string;
  kind?: string;
  rawInput?: string;
  output?: string;
  status?: string;
  rawOutput?: string;
  unifiedDiff?: string;
  diffPath?: string;
  diffOldText?: string;
  diffNewText?: string;
  startedAt?: string;
  endedAt?: string;
};

export const previewLines = 14;
/** Folding away fewer lines than this costs a tap for nothing, so short overflow is shown. */
export const minimumFold = 4;
const summaryChars = 72;
const agentUpPrefix = 'agent-up-';

const kindTitles: Record<string, string> = {
  read: 'Read', edit: 'Edit', delete: 'Delete', move: 'Move', search: 'Search',
  execute: 'Run', think: 'Think', fetch: 'Fetch', switch_mode: 'Switch mode',
};

const summaryKeys = ['command', 'filePath', 'file_path', 'pattern', 'query', 'path', 'url', 'description', 'workspaceId', 'worktreePath', 'name', 'id'];

export function presentToolCall(title?: string, kind?: string, rawInput?: string, output?: string, status?: string): ToolCallView {
  return presentToolFacts({ name: title, title, kind, rawInput, output, status });
}

export function presentToolFacts(facts: ToolCallFacts): ToolCallView {
  const { title, kind, status } = facts;
  const input = parseObject(facts.rawInput);
  const named = toolName(facts.name ?? title, kind);
  if (named.name === 'Edit' && isWrite(input)) named.name = 'Write';
  const summary = summaryOf(input) ?? pathLikeTitle(title, kind) ?? fileLikeTitle(title, facts.name) ?? named.server;
  // Some tools put what they read in their raw output and leave the visible content empty.
  const formatted = formatOutput(facts.output?.trim() ? facts.output : facts.rawOutput);
  const diff = diffOf(facts, input);
  const duration = durationBetween(facts.startedAt, facts.endedAt);
  const lines = formatted ? formatted.split('\n') : [];
  const folds = lines.length - previewLines >= minimumFold;
  const preview = folds ? lines.slice(0, previewLines).join('\n') : formatted;
  return {
    title: named.name,
    ...(named.source ? { source: named.source } : {}),
    ...(summary ? { summary: shorten(summary) } : {}),
    state: toolState(status),
    ...(input && Object.keys(input).length ? { input: JSON.stringify(input, null, 2) } : {}),
    ...(preview ? { output: preview } : {}),
    ...(formatted ? { fullOutput: formatted } : {}),
    hiddenLines: folds ? lines.length - previewLines : 0,
    ...(diff ? { diff } : {}),
    ...(duration ? { duration } : {}),
  };
}

/** The richest diff the agent gave: a unified diff, then old and new text, then a new file. */
export function diffOf(facts: ToolCallFacts, input = parseObject(facts.rawInput)): Diff | undefined {
  if (facts.unifiedDiff?.trim()) {
    const unified = diffFromUnified(facts.unifiedDiff, facts.diffPath);
    const path = facts.diffPath ?? summaryOf(input) ?? unified.path;
    if (unified.lines.length) return { ...unified, ...(path ? { path } : {}) };
  }
  if (facts.diffNewText !== undefined) return diffFromTexts(facts.diffOldText, facts.diffNewText, facts.diffPath);
  if (isWrite(input) && typeof input?.content === 'string') return diffNewFile(input.content, summaryOf(input));
  return undefined;
}

/** A step is only worth timing once it took a second; quick tools say nothing. */
export function durationBetween(startedAt?: string, endedAt?: string): string | undefined {
  if (!startedAt || !endedAt) return undefined;
  const seconds = (Date.parse(endedAt) - Date.parse(startedAt)) / 1000;
  if (!Number.isFinite(seconds) || seconds < 1) return undefined;
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const whole = Math.round(seconds);
  return `${Math.floor(whole / 60)}m ${whole % 60}s`;
}

function isWrite(input: Record<string, unknown> | undefined): boolean {
  return typeof input?.content === 'string' && !('oldString' in input) && !('old_string' in input);
}

/** A completed call is often retitled with the file it touched, when the tool name was something else. */
function fileLikeTitle(title?: string, name?: string): string | undefined {
  return title && name && title !== name && title.includes('.') && !title.includes(' ') ? title : undefined;
}

export function toolName(title?: string, kind?: string): { name: string; source?: string; server?: string } {
  const raw = title?.trim() ?? '';
  const separator = raw.indexOf('_');
  if (separator > 0 && !raw.includes(' ') && !raw.includes('/') && raw.slice(0, separator).includes('-')) {
    const server = raw.slice(0, separator);
    const tool = humanize(raw.slice(separator + 1));
    return server.startsWith(agentUpPrefix)
      ? { name: tool, source: 'Agent-Up', server: humanize(server.slice(agentUpPrefix.length)).toLowerCase() }
      : { name: tool, source: humanize(server) };
  }
  const named = kind ? kindTitles[kind.toLowerCase()] : undefined;
  if (named && (!raw || looksLikePath(raw) || kind?.toLowerCase() !== 'other')) return { name: named };
  return { name: raw ? humanize(raw) : 'Tool' };
}

export function toolState(status?: string): ToolCallState {
  return status === 'completed' ? 'done' : status === 'failed' ? 'failed' : 'running';
}

export function formatOutput(output?: string): string | undefined {
  const trimmed = output?.trim();
  if (!trimmed) return undefined;
  // OpenCode wraps what a read returned in tags; the reader wants the file text.
  const read = /^<path>[^<]*<\/path>\s*(?:<type>[^<]*<\/type>\s*)?<content>([\s\S]*)<\/content>$/.exec(trimmed);
  if (read) return read[1]!.trim() ? read[1]!.replace(/^\n+|\n+$/g, '') : undefined;
  if (trimmed[0] === '{' || trimmed[0] === '[') {
    try { return JSON.stringify(JSON.parse(trimmed), null, 2); }
    catch { return trimmed; }
  }
  return trimmed;
}

/** Keeps a long path or command on one line by cutting the middle, where it says least. */
export function shorten(value: string): string {
  const line = value.replace(/\s*\n\s*/g, ' ').trim();
  if (line.length <= summaryChars) return line;
  const keep = Math.floor((summaryChars - 1) / 2);
  return `${line.slice(0, keep)}…${line.slice(line.length - keep)}`;
}

function summaryOf(input: Record<string, unknown> | undefined): string | undefined {
  if (!input) return undefined;
  return summaryKeys.map(key => scalar(input[key])).find(isTelling) ?? Object.values(input).map(scalar).find(isTelling);
}

/** "." or "./" says nothing about a call, so a search names its pattern instead of its directory. */
function isTelling(value: string | undefined): value is string {
  return Boolean(value) && value !== '.' && value !== './';
}

function pathLikeTitle(title?: string, kind?: string) {
  return title && kind && looksLikePath(title) ? title : undefined;
}

function looksLikePath(value: string) { return value.includes('/') || value.includes('\\'); }

function scalar(value: unknown): string | undefined {
  if (typeof value === 'string') return value || undefined;
  if (typeof value === 'number' || typeof value === 'boolean') return String(value);
  return undefined;
}

function parseObject(json?: string): Record<string, unknown> | undefined {
  if (!json?.trim()) return undefined;
  try {
    const value = JSON.parse(json);
    return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : undefined;
  } catch {
    return undefined;
  }
}

/** `list_workspaces` and `listWorkspaces` both read "List workspaces". */
function humanize(name: string): string {
  const words = name.replace(/([a-z0-9])([A-Z])/g, '$1 $2').split(/[_\-. ]+/).filter(Boolean);
  if (!words.length) return name;
  const sentence = words.map(word => word.toLowerCase()).join(' ');
  return sentence[0].toUpperCase() + sentence.slice(1);
}
