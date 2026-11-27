import type { TranscriptItem } from '../models/AgentSession';
import { thoughtLabel } from './AgentEventPresentationProvider';
import { humanize, parseObject, presentToolFacts, shorten } from './ToolCallProvider';

/**
 * Geometry and gesture decisions for the bottom sheet one transcript item opens into. Kept free of
 * React Native so the rules a finger runs into are tested under plain Node.
 *
 * Heights are how much of the sheet shows above the bottom of the window. A sheet rests at one of
 * two detents - half for a glance, full for reading - or closes.
 */

export type SheetDetent = 'half' | 'full';
export type SheetRest = SheetDetent | 'closed';
export type SheetDetents = { half: number; full: number };

/** Space kept clear above a full sheet, so the screen behind still shows it is a sheet. */
export const sheetTopGap = 12;
/** How far a drag must move before it counts as one, so a tap stays a tap. */
export const dragSlop = 6;
/** Release speed, in points per millisecond, that reads as a flick rather than a placement. */
export const flickVelocity = 0.9;
/** How far past full a sheet follows the finger, as a fraction of the overshoot. */
export const overshootResistance = 0.2;
/**
 * Largest common phone CSS width. A surface wider than this is past the mobile format: a tap
 * unfolds the item in place instead of opening a sheet.
 */
export const compactLayoutWidth = 430;

/** Whether this surface still uses a bottom sheet for a tapped tool call or thought. */
export function usesSheet(width: number): boolean {
  return width <= compactLayoutWidth;
}

/** Half and full heights for a window, below the status bar or notch. */
export function sheetDetents(windowHeight: number, topInset: number): SheetDetents {
  const full = Math.max(0, windowHeight - topInset - sheetTopGap);
  return { half: Math.min(full, Math.round(windowHeight * 0.5)), full };
}

export function detentHeight(detent: SheetDetent, detents: SheetDetents): number {
  return detent === 'full' ? detents.full : detents.half;
}

/**
 * Whether a move inside the sheet belongs to the sheet rather than its content. Sideways moves stay
 * with the content (a diff scrolls sideways). At half height every vertical drag moves the sheet,
 * which is how pulling up enlarges it. At full height the content scrolls, and the sheet only takes a
 * downward drag once the content is back at its top.
 */
export function sheetTakesDrag(input: { detent: SheetDetent; scrollTop: number; dx: number; dy: number }): boolean {
  const { detent, scrollTop, dx, dy } = input;
  if (Math.abs(dy) < dragSlop || Math.abs(dy) <= Math.abs(dx)) return false;
  if (detent === 'half') return true;
  return dy > 0 && scrollTop <= 0;
}

/** The height a sheet shows while a finger drags it from `start` by `dy` (down is positive). */
export function draggedHeight(start: number, dy: number, detents: SheetDetents): number {
  const raw = start - dy;
  if (raw <= 0) return 0;
  if (raw <= detents.full) return raw;
  return detents.full + (raw - detents.full) * overshootResistance;
}

/**
 * Where a released sheet comes to rest. A flick goes one detent in its direction - down from half
 * closes - and a slow release settles at whichever of closed, half and full is nearest.
 */
export function settleSheet(input: { height: number; velocity: number; from: SheetDetent; detents: SheetDetents }): SheetRest {
  const { height, velocity, from, detents } = input;
  if (velocity >= flickVelocity) return from === 'full' && height > detents.half ? 'half' : 'closed';
  if (velocity <= -flickVelocity) return 'full';
  const candidates: [SheetRest, number][] = [['closed', 0], ['half', detents.half], ['full', detents.full]];
  return candidates.reduce((best, candidate) => Math.abs(candidate[1] - height) < Math.abs(best[1] - height) ? candidate : best)[0];
}

/**
 * Whether a transcript item can open as a detail. Only the lowest level does - a tool call, an
 * edit, a thought. Sections that group items unfold in place, and prose and plans are already in
 * full. A phone-width surface opens that detail in a sheet; a wider one unfolds it in place.
 */
export function opensInSheet(item: Pick<TranscriptItem, 'role'>): boolean {
  return item.role === 'tool' || item.role === 'thought';
}

export type SheetArgument = { label: string; value: string };
export type SheetStatusTone = 'done' | 'failed' | 'running';
export type SheetPresentation = {
  /** What the row did, as a sentence: "Edit products.ts", "Run npm test", "Thought for 3s". */
  title: string;
  /** How it went, under the title: "✕ Failed · 4s", "✓ Done · Github api", "Running". */
  status?: { tone: SheetStatusTone; text: string };
  /** The call's arguments as labelled values, without the one the title already says. Never raw JSON. */
  arguments: SheetArgument[];
};

/** Arguments an edit carries that its diff already shows better. */
const bodyKeys = new Set(['content', 'oldString', 'old_string', 'newString', 'new_string', 'edits', 'replaceAll', 'replace_all']);
const maxArguments = 8;
const statusWords: Record<SheetStatusTone, string> = { done: '✓ Done', failed: '✕ Failed', running: 'Running' };

/** How one transcript item reads once opened: a sentence for a title, its status, and labelled arguments. */
export function presentSheet(item: TranscriptItem, live: boolean): SheetPresentation {
  if (item.role === 'thought') return { title: thoughtLabel(item, live), arguments: [] };
  const view = presentToolFacts({ ...item, kind: item.toolKind });
  const path = view.summary && /[\\/]/.test(view.summary) && !/\s/.test(view.summary) ? view.summary : undefined;
  const subject = path ? path.split(/[\\/]/).filter(Boolean).at(-1) : view.summary;
  const title = subject && subject !== view.title ? `${view.title} ${subject}` : view.title;
  const tone: SheetStatusTone = view.state === 'done' ? 'done' : view.state;
  const text = [statusWords[tone], view.state === 'running' ? undefined : view.duration, view.source].filter(Boolean).join(' · ');
  const details = sheetArguments(item.rawInput, view.summary, Boolean(view.diff));
  // The diff card names the file; without one, the whole path is a detail worth showing - unless
  // the title shortened it, and the argument it came from is already a detail in full.
  const fullPathShown = details.some(row => view.summary !== undefined && row.value !== view.summary && shorten(row.value) === view.summary);
  const pathRow = path && !view.diff && !fullPathShown ? [{ label: 'Path', value: path }] : [];
  return { title, status: { tone, text }, arguments: dedupe([...pathRow, ...details]) };
}

function dedupe(rows: SheetArgument[]): SheetArgument[] {
  const seen = new Set<string>();
  return rows.filter(row => !seen.has(row.label) && seen.add(row.label));
}

function sheetArguments(rawInput: string | undefined, summary: string | undefined, hasDiff: boolean): SheetArgument[] {
  const input = parseObject(rawInput);
  if (!input) return [];
  const rows: SheetArgument[] = [];
  for (const [key, raw] of Object.entries(input)) {
    if (raw === null || raw === undefined || raw === '') continue;
    if (hasDiff && bodyKeys.has(key)) continue;
    const value = typeof raw === 'string' ? raw : typeof raw === 'number' || typeof raw === 'boolean' ? String(raw) : JSON.stringify(raw);
    // The title already says this value only when it says all of it; a shortened one stays.
    if (summary && value === summary) continue;
    // Kept whole: the sheet wraps it, and this is the one place the reader can see all of it.
    rows.push({ label: humanize(key), value });
    if (rows.length === maxArguments) break;
  }
  return rows;
}
