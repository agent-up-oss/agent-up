/**
 * Turns what a coding agent reports about an edit into diff lines. It mirrors Desktop's
 * AgentDiffProvider: a unified diff (OpenCode's tool metadata), an old and new text pair (ACP's
 * diff content, often only the replaced snippet), and a new file's content.
 */
export type DiffLineKind = 'added' | 'deleted' | 'context' | 'hunk';
export type DiffLine = { kind: DiffLineKind; oldNumber?: number; newNumber?: number; text: string };
export type Diff = { path?: string; lines: DiffLine[]; added: number; removed: number };

/** Unchanged lines kept around each change when two texts are compared. */
export const diffContext = 3;
/** Beyond this many lines a side, two texts are shown as a whole replacement. */
export const compareLimit = 600;

const hunkPattern = /^@@\s+-(\d+)(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s+@@/;

export function diffFromUnified(unified: string, path?: string): Diff {
  const lines: DiffLine[] = [];
  let oldLine = 0;
  let newLine = 0;
  let file = path;
  for (const raw of unified.replace(/\r\n/g, '\n').split('\n')) {
    const hunk = hunkPattern.exec(raw);
    if (hunk) {
      oldLine = Number(hunk[1]);
      newLine = Number(hunk[2]);
      lines.push({ kind: 'hunk', text: raw });
      continue;
    }
    if (raw.startsWith('+++ ')) { file ??= raw.slice(4).trim(); continue; }
    if (!lines.length || isHeader(raw)) continue;
    if (raw.startsWith('+')) lines.push({ kind: 'added', newNumber: newLine++, text: raw.slice(1) });
    else if (raw.startsWith('-')) lines.push({ kind: 'deleted', oldNumber: oldLine++, text: raw.slice(1) });
    else if (raw.startsWith(' ')) lines.push({ kind: 'context', oldNumber: oldLine++, newNumber: newLine++, text: raw.slice(1) });
  }
  return counted(file, lines);
}

export function diffNewFile(content: string, path?: string): Diff {
  return counted(path, splitLines(content).map((text, index) => ({ kind: 'added', newNumber: index + 1, text })));
}

export function diffFromTexts(oldText: string | undefined, newText: string, path?: string): Diff {
  const before = oldText === undefined ? [] : splitLines(oldText);
  const after = splitLines(newText);
  if (before.length > compareLimit || after.length > compareLimit) {
    return counted(path, [
      ...before.map((text, index): DiffLine => ({ kind: 'deleted', oldNumber: index + 1, text })),
      ...after.map((text, index): DiffLine => ({ kind: 'added', newNumber: index + 1, text })),
    ]);
  }
  const edits = compare(before, after);
  const keep = edits.map(() => false);
  edits.forEach((edit, index) => {
    if (edit.kind === 'context') return;
    for (let near = Math.max(0, index - diffContext); near <= Math.min(edits.length - 1, index + diffContext); near++) keep[near] = true;
  });
  const lines: DiffLine[] = [];
  edits.forEach((edit, index) => {
    if (!keep[index]) return;
    if (lines.length && !keep[index - 1]) lines.push({ kind: 'hunk', text: '…' });
    lines.push(edit);
  });
  return counted(path, lines);
}

/** Longest-common-subsequence line diff: deletions before additions within a change. */
function compare(before: string[], after: string[]): DiffLine[] {
  const table = Array.from({ length: before.length + 1 }, () => new Array<number>(after.length + 1).fill(0));
  for (let left = before.length - 1; left >= 0; left--) {
    for (let right = after.length - 1; right >= 0; right--) {
      table[left]![right] = before[left] === after[right] ? table[left + 1]![right + 1]! + 1 : Math.max(table[left + 1]![right]!, table[left]![right + 1]!);
    }
  }
  const lines: DiffLine[] = [];
  let i = 0;
  let j = 0;
  while (i < before.length || j < after.length) {
    if (i < before.length && j < after.length && before[i] === after[j]) {
      lines.push({ kind: 'context', oldNumber: i + 1, newNumber: j + 1, text: before[i]! });
      i++; j++;
    } else if (j === after.length || (i < before.length && table[i + 1]![j]! >= table[i]![j + 1]!)) {
      lines.push({ kind: 'deleted', oldNumber: i + 1, text: before[i]! });
      i++;
    } else {
      lines.push({ kind: 'added', newNumber: j + 1, text: after[j]! });
      j++;
    }
  }
  return lines;
}

function counted(path: string | undefined, lines: DiffLine[]): Diff {
  return {
    ...(path ? { path } : {}),
    lines,
    added: lines.filter(line => line.kind === 'added').length,
    removed: lines.filter(line => line.kind === 'deleted').length,
  };
}

function splitLines(text: string): string[] {
  let normalized = text.replace(/\r\n/g, '\n');
  if (normalized.endsWith('\n')) normalized = normalized.slice(0, -1);
  return normalized ? normalized.split('\n') : [];
}

function isHeader(line: string): boolean {
  return line.startsWith('--- ') || line.startsWith('Index: ') || line.startsWith('====') || line.startsWith('diff --git') || line.startsWith('index ') || line.startsWith('\\ ');
}
