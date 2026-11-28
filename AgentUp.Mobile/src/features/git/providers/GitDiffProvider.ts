import type { SyntaxToken } from '@agent-up/design-system/syntax';

/**
 * A changed file read in place, the way a code host shows it: the hunks of a unified diff become
 * runs of numbered lines, the unchanged stretches between them fold into rows that open five lines
 * at a time from the file itself, and a removed line paired with the added line that replaced it
 * marks only the words that changed. No `@@` header ever reaches the screen. Kept free of React
 * Native so every rule here is tested under plain Node.
 */

export type DiffLineKind = 'context' | 'added' | 'deleted';

/** A run of text inside a changed line, and whether it is part of what changed. */
export type DiffSpan = { text: string; changed: boolean };

export type DiffLine = {
  kind: DiffLineKind;
  oldNumber: number | null;
  newNumber: number | null;
  text: string;
  /** The words of an edited line, when it pairs with the line it replaced or that replaced it. */
  words?: DiffSpan[];
};

export type DiffHunk = {
  oldStart: number;
  oldCount: number;
  newStart: number;
  newCount: number;
  lines: DiffLine[];
};

export type ParsedDiff = { hunks: DiffHunk[]; added: number; deleted: number };

/** Unchanged lines folded away, as a range of the new file. */
export type DiffFold = {
  id: string;
  /** First and last hidden line, numbered in the new file. */
  from: number;
  to: number;
  hidden: number;
  /** Old line number minus new line number across this stretch. */
  offset: number;
  /** Whether lines can be shown continuing down from the run above, or up from the run below. */
  opensDown: boolean;
  opensUp: boolean;
};

export type DiffSegment =
  | { kind: 'lines'; lines: DiffLine[] }
  | { kind: 'fold'; fold: DiffFold };

/** How much of a fold is shown: lines continuing down from the run above, and up from the run below. */
export type DiffReveal = Record<string, { down: number; up: number }>;

/** How many lines one tap on a fold shows. */
export const DIFF_EXPAND_STEP = 5;
/** Row heights the view draws. */
export const DIFF_LINE_HEIGHT = 20;
export const DIFF_FOLD_HEIGHT = 34;

const HUNK = /^@@\s+-(\d+)(?:,(\d+))?\s+\+(\d+)(?:,(\d+))?\s+@@/;
/** Above this many token pairs a line is too long to compare word by word cheaply. */
const WORD_DIFF_BUDGET = 40000;
/** Below this share of text in common, an edited line reads as replaced rather than edited. */
const WORD_DIFF_SIMILARITY = 0.35;

export function parseUnifiedDiff(diff: string | null | undefined): ParsedDiff {
  const hunks: DiffHunk[] = [];
  let added = 0;
  let deleted = 0;
  if (!diff) return { hunks, added, deleted };
  let current: DiffHunk | null = null;
  let oldNumber = 0;
  let newNumber = 0;
  for (const raw of diff.split('\n')) {
    const text = raw.endsWith('\r') ? raw.slice(0, -1) : raw;
    const header = HUNK.exec(text);
    if (header) {
      current = {
        oldStart: Number(header[1]),
        oldCount: header[2] === undefined ? 1 : Number(header[2]),
        newStart: Number(header[3]),
        newCount: header[4] === undefined ? 1 : Number(header[4]),
        lines: [],
      };
      hunks.push(current);
      oldNumber = current.oldStart;
      newNumber = current.newStart;
      continue;
    }
    // Everything before the first hunk is the diff's own header, and `\ No newline` is a note.
    if (!current || text.startsWith('\\')) continue;
    if (text.startsWith('+')) {
      current.lines.push({ kind: 'added', oldNumber: null, newNumber, text: text.slice(1) });
      newNumber += 1;
      added += 1;
    } else if (text.startsWith('-')) {
      current.lines.push({ kind: 'deleted', oldNumber, newNumber: null, text: text.slice(1) });
      oldNumber += 1;
      deleted += 1;
    } else if (text.startsWith(' ') || (text === '' && oldNumber < current.oldStart + current.oldCount)) {
      current.lines.push({ kind: 'context', oldNumber, newNumber, text: text.slice(1) });
      oldNumber += 1;
      newNumber += 1;
    }
  }
  for (const hunk of hunks) pairEditedLines(hunk.lines);
  return { hunks, added, deleted };
}

/**
 * Marks the words an edited line changed. Inside each run of removed lines followed by added ones,
 * the n-th removed line pairs with the n-th added line when they share enough text to be the same
 * line edited; a pair that shares too little stays whole, because marking every word says nothing.
 */
export function pairEditedLines(lines: DiffLine[]): void {
  let index = 0;
  while (index < lines.length) {
    if (lines[index].kind !== 'deleted') { index += 1; continue; }
    const removedStart = index;
    while (index < lines.length && lines[index].kind === 'deleted') index += 1;
    const addedStart = index;
    while (index < lines.length && lines[index].kind === 'added') index += 1;
    const pairs = Math.min(addedStart - removedStart, index - addedStart);
    for (let pair = 0; pair < pairs; pair += 1) {
      const removed = lines[removedStart + pair];
      const added = lines[addedStart + pair];
      const words = wordDiff(removed.text, added.text);
      if (!words) continue;
      removed.words = words.before;
      added.words = words.after;
    }
  }
}

/** The words two versions of a line share and do not, or null when they are not one line edited. */
export function wordDiff(before: string, after: string): { before: DiffSpan[]; after: DiffSpan[] } | null {
  if (before === after) return null;
  const a = tokens(before);
  const b = tokens(after);
  if (a.length * b.length > WORD_DIFF_BUDGET) return null;
  const table: number[][] = Array.from({ length: a.length + 1 }, () => new Array<number>(b.length + 1).fill(0));
  for (let i = a.length - 1; i >= 0; i -= 1) {
    for (let j = b.length - 1; j >= 0; j -= 1) {
      table[i][j] = a[i] === b[j] ? table[i + 1][j + 1] + 1 : Math.max(table[i + 1][j], table[i][j + 1]);
    }
  }
  const left: DiffSpan[] = [];
  const right: DiffSpan[] = [];
  let i = 0;
  let j = 0;
  let shared = 0;
  while (i < a.length || j < b.length) {
    if (i < a.length && j < b.length && a[i] === b[j]) {
      push(left, a[i], false);
      push(right, b[j], false);
      shared += a[i].trim() ? a[i].length : 0;
      i += 1;
      j += 1;
    } else if (j < b.length && (i >= a.length || table[i][j + 1] >= table[i + 1][j])) {
      push(right, b[j], true);
      j += 1;
    } else {
      push(left, a[i], true);
      i += 1;
    }
  }
  const visible = before.replace(/\s/g, '').length + after.replace(/\s/g, '').length;
  if (visible === 0 || (2 * shared) / visible < WORD_DIFF_SIMILARITY) return null;
  return { before: tidy(left), after: tidy(right) };
}

/**
 * The file as runs of shown lines and folds of hidden ones. `file` is the new file's lines, which a
 * fold needs to show what it hides; without it the folds between hunks still say how much they
 * hide, and the stretch after the last hunk, whose length is unknown, is left out.
 */
export function diffSegments(parsed: ParsedDiff, file: readonly string[] | null, reveal: DiffReveal = {}): DiffSegment[] {
  const segments: DiffSegment[] = [];
  const addLines = (lines: DiffLine[]) => {
    if (!lines.length) return;
    const last = segments[segments.length - 1];
    if (last?.kind === 'lines') last.lines.push(...lines);
    else segments.push({ kind: 'lines', lines: [...lines] });
  };
  const addGap = (id: string, from: number, to: number, offset: number, hasAbove: boolean, hasBelow: boolean) => {
    const size = to - from + 1;
    if (size <= 0) return;
    const opens = file !== null;
    const shown = reveal[id] ?? { down: 0, up: 0 };
    const down = opens && hasAbove ? Math.min(shown.down, size) : 0;
    const up = opens ? Math.min(shown.up, size - down) : 0;
    addLines(contextLines(file, from, from + down - 1, offset));
    const hidden = size - down - up;
    if (hidden > 0) {
      segments.push({
        kind: 'fold',
        fold: { id, from: from + down, to: to - up, hidden, offset, opensDown: opens && hasAbove, opensUp: opens && hasBelow },
      });
    }
    addLines(contextLines(file, to - up + 1, to, offset));
  };

  let previous: DiffHunk | null = null;
  parsed.hunks.forEach((hunk, index) => {
    const from = previous ? lastNew(previous) + 1 : 1;
    addGap(`fold-${index}`, from, firstNew(hunk) - 1, firstOld(hunk) - firstNew(hunk), previous !== null, true);
    addLines(hunk.lines);
    previous = hunk;
  });
  if (previous && file) {
    const last: DiffHunk = previous;
    addGap(`fold-${parsed.hunks.length}`, lastNew(last) + 1, file.length, lastOld(last) - lastNew(last), true, false);
  }
  return segments;
}

/** Shows `count` more lines of a fold, continuing down from the run above or up from the run below. */
export function revealMore(reveal: DiffReveal, fold: DiffFold, direction: 'down' | 'up' | 'all', count = DIFF_EXPAND_STEP): DiffReveal {
  const shown = reveal[fold.id] ?? { down: 0, up: 0 };
  if (direction === 'all') return { ...reveal, [fold.id]: { down: shown.down, up: shown.up + fold.hidden } };
  const step = Math.min(count, fold.hidden);
  return { ...reveal, [fold.id]: direction === 'down' ? { ...shown, down: shown.down + step } : { ...shown, up: shown.up + step } };
}

/** The number a line shows: where it is now, or where it was for a line that is gone. */
export function diffLineNumber(line: DiffLine): number | null {
  return line.kind === 'deleted' ? line.oldNumber : line.newNumber;
}

/** The file named the way an editor tab names it: its kind, its name, and the folder it is in. */
export function diffFileLabel(path: string): { kind: string; name: string; folder: string } {
  const slash = path.lastIndexOf('/');
  const name = slash >= 0 ? path.slice(slash + 1) : path;
  const folder = slash >= 0 ? path.slice(0, slash) : '';
  const dot = name.lastIndexOf('.');
  const extension = dot > 0 ? name.slice(dot + 1) : '';
  return { kind: (extension || 'txt').slice(0, 4).toUpperCase(), name, folder };
}

/** What the view says instead of lines, or null when there are lines to show. */
export function diffMessage(diff: { isBinary: boolean; diff: string } | null, path: string): string | null {
  if (!diff) return 'This file no longer has changes.';
  if (diff.isBinary) return 'This file is binary; Agent-Up does not render a text diff for it.';
  if (!diff.diff.trim()) return `${path} has no text to show.`;
  return null;
}

/**
 * Syntax colours and changed words over the same text: each piece keeps its token's colour and is
 * marked when it falls inside a changed word.
 */
export function paintedPieces(tokens: SyntaxToken[], words: DiffSpan[] | undefined): Array<{ text: string; kind: string; changed: boolean }> {
  if (!words) return tokens.map(token => ({ text: token.text, kind: token.kind, changed: false }));
  const pieces: Array<{ text: string; kind: string; changed: boolean }> = [];
  let word = 0;
  let wordLeft = words[0]?.text.length ?? 0;
  for (const token of tokens) {
    let rest = token.text;
    while (rest.length) {
      while (word < words.length && wordLeft === 0) { word += 1; wordLeft = words[word]?.text.length ?? 0; }
      const changed = words[word]?.changed ?? false;
      const take = word < words.length ? Math.min(rest.length, wordLeft) : rest.length;
      const text = rest.slice(0, take);
      const last = pieces[pieces.length - 1];
      if (last && last.kind === token.kind && last.changed === changed) last.text += text;
      else pieces.push({ text, kind: token.kind, changed });
      rest = rest.slice(take);
      wordLeft -= take;
    }
  }
  return pieces;
}

/** The catalog text style a syntax token kind is painted with. */
export function syntaxClassName(kind: string): string {
  switch (kind) {
    case 'keyword': return 'syntaxKeyword';
    case 'type': return 'syntaxType';
    case 'string': return 'syntaxString';
    case 'comment': return 'syntaxComment';
    case 'number': return 'syntaxNumber';
    case 'function': return 'syntaxFunction';
    case 'property': return 'syntaxProperty';
    case 'punctuation': return 'syntaxPunctuation';
    case 'operator': return 'syntaxOperator';
    default: return 'syntaxPlain';
  }
}

/** A file's text as its lines, the way a diff numbers them. */
export function fileLines(content: string): string[] {
  const lines = content.split('\n').map(line => (line.endsWith('\r') ? line.slice(0, -1) : line));
  if (lines.length > 1 && lines[lines.length - 1] === '') lines.pop();
  return lines;
}

function contextLines(file: readonly string[] | null, from: number, to: number, offset: number): DiffLine[] {
  if (!file || to < from) return [];
  const lines: DiffLine[] = [];
  for (let number = from; number <= to; number += 1) {
    lines.push({ kind: 'context', oldNumber: number + offset, newNumber: number, text: file[number - 1] ?? '' });
  }
  return lines;
}

function firstNew(hunk: DiffHunk): number { return hunk.newCount === 0 ? hunk.newStart + 1 : hunk.newStart; }
function lastNew(hunk: DiffHunk): number { return hunk.newCount === 0 ? hunk.newStart : hunk.newStart + hunk.newCount - 1; }
function firstOld(hunk: DiffHunk): number { return hunk.oldCount === 0 ? hunk.oldStart + 1 : hunk.oldStart; }
function lastOld(hunk: DiffHunk): number { return hunk.oldCount === 0 ? hunk.oldStart : hunk.oldStart + hunk.oldCount - 1; }

function tokens(text: string): string[] {
  return text.match(/\w+|\s+|[^\w\s]/g) ?? [];
}

function push(spans: DiffSpan[], text: string, changed: boolean) {
  const last = spans[spans.length - 1];
  if (last && last.changed === changed) last.text += text;
  else spans.push({ text, changed });
}

/** Whitespace between two changed runs joins them, so a changed phrase reads as one mark. */
function tidy(spans: DiffSpan[]): DiffSpan[] {
  const joined: DiffSpan[] = [];
  spans.forEach((span, index) => {
    const before = joined[joined.length - 1];
    const after = spans[index + 1];
    const bridge = !span.changed && !span.text.trim() && before?.changed && after?.changed;
    const changed = span.changed || Boolean(bridge);
    if (before && before.changed === changed) before.text += span.text;
    else joined.push({ text: span.text, changed });
  });
  return joined;
}
