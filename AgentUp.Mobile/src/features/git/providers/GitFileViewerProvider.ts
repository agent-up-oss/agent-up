import { detectLanguage, tokenizeLine, type SyntaxToken } from '@agent-up/design-system/syntax';
import type { GitFileDiff } from '../models/GitChanges';

export const FILE_VIEWER_LINE_HEIGHT = 22;
export const FILE_VIEWER_WINDOW = 40;

export type FileViewerLineKind = 'meta' | 'hunk' | 'context' | 'added' | 'deleted';

export type FileViewerLine = {
  index: number;
  kind: FileViewerLineKind;
  oldNumber: number | null;
  newNumber: number | null;
  prefix: string;
  text: string;
};

export type FileViewerHunk = {
  label: string;
  lineIndex: number;
};

const HUNK = /^@@\s+-(\d+)(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s@@/;

export function parseFileDiff(diff: string | null | undefined): FileViewerLine[] {
  if (!diff) return [];
  const raw = splitDiffLines(diff);
  const lines: FileViewerLine[] = [];
  let oldNumber = 0;
  let newNumber = 0;

  for (const text of raw) {
    const index = lines.length;
    if (text.startsWith('@@')) {
      const match = HUNK.exec(text);
      if (match) {
        oldNumber = Number.parseInt(match[1], 10);
        newNumber = Number.parseInt(match[2], 10);
      }
      lines.push({ index, kind: 'hunk', oldNumber: null, newNumber: null, prefix: ' ', text });
      continue;
    }
    if (isMeta(text)) {
      lines.push({ index, kind: 'meta', oldNumber: null, newNumber: null, prefix: ' ', text });
      continue;
    }
    if (text.startsWith('+')) {
      lines.push({
        index,
        kind: 'added',
        oldNumber: null,
        newNumber,
        prefix: '+',
        text: text.slice(1),
      });
      newNumber += 1;
      continue;
    }
    if (text.startsWith('-')) {
      lines.push({
        index,
        kind: 'deleted',
        oldNumber,
        newNumber: null,
        prefix: '−',
        text: text.slice(1),
      });
      oldNumber += 1;
      continue;
    }
    const body = text.startsWith(' ') ? text.slice(1) : text;
    lines.push({
      index,
      kind: 'context',
      oldNumber,
      newNumber,
      prefix: ' ',
      text: body,
    });
    oldNumber += 1;
    newNumber += 1;
  }

  return lines;
}

export function fileViewerHunks(lines: FileViewerLine[]): FileViewerHunk[] {
  return lines
    .filter(line => line.kind === 'hunk')
    .map(line => ({ label: line.text, lineIndex: line.index }));
}

export function splitDiffLines(diff: string): string[] {
  const lines: string[] = [];
  let start = 0;
  for (let index = 0; index < diff.length; index += 1) {
    if (diff.charCodeAt(index) !== 10) continue;
    const end = index > start && diff.charCodeAt(index - 1) === 13 ? index - 1 : index;
    lines.push(diff.slice(start, end));
    start = index + 1;
  }
  if (start < diff.length) lines.push(diff.slice(start));
  else if (diff.length > 0 && diff.charCodeAt(diff.length - 1) === 10 && lines.length === 0) {
    lines.push('');
  }
  return lines;
}

export function highlightLine(path: string, line: FileViewerLine): SyntaxToken[] {
  if (line.kind === 'hunk' || line.kind === 'meta') return [{ kind: 'plain', text: line.text }];
  return tokenizeLine(line.text, detectLanguage(path));
}

export function windowedLines(lines: FileViewerLine[], start: number, size = FILE_VIEWER_WINDOW): FileViewerLine[] {
  const begin = Math.max(0, start);
  return lines.slice(begin, begin + Math.max(1, size));
}

export function jumpIndex(lines: FileViewerLine[], query: string): number | null {
  const trimmed = query.trim();
  if (!trimmed) return null;
  const asNumber = Number.parseInt(trimmed, 10);
  if (Number.isFinite(asNumber) && String(asNumber) === trimmed) {
    const match = lines.find(line => line.newNumber === asNumber || line.oldNumber === asNumber);
    return match ? match.index : null;
  }
  const hunk = lines.find(line => line.kind === 'hunk' && line.text.includes(trimmed));
  return hunk ? hunk.index : null;
}

export function fileViewerMessage(diff: GitFileDiff | null, path: string): string | null {
  if (!diff) return 'This file no longer has changes.';
  if (diff.isBinary) return 'This file is binary; Agent-Up does not render a text diff for it.';
  if (!diff.diff) return `${path} has no text to inspect.`;
  return null;
}

export function lineBoxNames(kind: FileViewerLineKind, current: boolean): string[] {
  const names = ['fileViewerLine'];
  if (kind === 'added') names.push('fileViewerLineAdded');
  if (kind === 'deleted') names.push('fileViewerLineDeleted');
  if (kind === 'hunk') names.push('fileViewerLineHunk');
  if (kind === 'meta') names.push('fileViewerLineMeta');
  if (current) names.push('fileViewerLineCurrent');
  return names;
}

export function prefixClassName(kind: FileViewerLineKind): string {
  if (kind === 'added') return 'fileViewerPrefixAdded';
  if (kind === 'deleted') return 'fileViewerPrefixDeleted';
  return 'fileViewerPrefix';
}

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

function isMeta(text: string): boolean {
  return text.startsWith('diff --git')
    || text.startsWith('index ')
    || text.startsWith('--- ')
    || text.startsWith('+++ ')
    || text.startsWith('new file')
    || text.startsWith('deleted file')
    || text.startsWith('similarity index')
    || text.startsWith('rename ')
    || text.startsWith('\\ ');
}
