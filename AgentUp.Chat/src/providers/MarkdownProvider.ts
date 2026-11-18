/**
 * Reads the markdown an agent writes into blocks and inline runs the transcript can draw. It
 * mirrors Desktop's AgentMarkdownProvider rule for rule: headings, paragraphs, bullet and
 * numbered lists, quotes, rules, fenced code, inline code, emphasis and links. It tolerates a
 * reply that is still streaming: an unclosed fence is code so far, and unmatched emphasis
 * markers stay literal text.
 */
export type MarkdownInlineKind = 'text' | 'bold' | 'italic' | 'code' | 'link';
export type MarkdownInline = { kind: MarkdownInlineKind; text: string; url?: string };
export type MarkdownBlock =
  | { kind: 'paragraph'; inlines: MarkdownInline[] }
  | { kind: 'heading'; level: number; inlines: MarkdownInline[] }
  | { kind: 'listItem'; depth: number; marker: string; inlines: MarkdownInline[] }
  | { kind: 'quote'; inlines: MarkdownInline[] }
  | { kind: 'code'; code: string; language?: string }
  | { kind: 'rule' };

const fencePattern = /^(`{3,}|~{3,})([^`]*)$/;
const rulePattern = /^(?:-{3,}|\*{3,}|_{3,})$/;
const headingPattern = /^(#{1,6})\s+(.*)$/;
const listPattern = /^([ \t]*)([-*+]|\d{1,3}[.)])\s+(.*)$/;
const linkPattern = /^\[([^\]]+)\]\(([^)\s]+)\)/;

export function parseMarkdown(markdown: string | null | undefined): MarkdownBlock[] {
  const blocks: MarkdownBlock[] = [];
  if (!markdown || !markdown.trim()) return blocks;
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  let paragraph: string[] = [];
  const flush = () => {
    if (!paragraph.length) return;
    blocks.push({ kind: 'paragraph', inlines: parseInlines(paragraph.join(' ')) });
    paragraph = [];
  };

  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    const trimmed = line.trim();
    const fence = fencePattern.exec(trimmed);
    if (fence) {
      flush();
      const marker = fence[1];
      const language = fence[2].trim();
      const code: string[] = [];
      for (index++; index < lines.length && !lines[index].trim().startsWith(marker); index++) code.push(lines[index]);
      blocks.push({ kind: 'code', code: code.join('\n').trimEnd(), ...(language ? { language } : {}) });
      continue;
    }
    if (!trimmed) { flush(); continue; }
    if (rulePattern.test(trimmed)) { flush(); blocks.push({ kind: 'rule' }); continue; }
    const heading = headingPattern.exec(trimmed);
    if (heading) {
      flush();
      blocks.push({ kind: 'heading', level: heading[1].length, inlines: parseInlines(heading[2].replace(/[#\s]+$/, '')) });
      continue;
    }
    const item = listPattern.exec(line);
    if (item) {
      flush();
      const depth = Math.floor(item[1].replace(/\t/g, '  ').length / 2);
      const bullet = item[2];
      const marker = /\d/.test(bullet[0]) ? `${bullet.replace(/[.)]$/, '')}.` : '•';
      blocks.push({ kind: 'listItem', depth, marker, inlines: parseInlines(item[3]) });
      continue;
    }
    if (trimmed.startsWith('>')) {
      flush();
      const quote = [trimmed.replace(/^>+/, '').trim()];
      while (index + 1 < lines.length && lines[index + 1].trimStart().startsWith('>')) quote.push(lines[++index].trim().replace(/^>+/, '').trim());
      blocks.push({ kind: 'quote', inlines: parseInlines(quote.join(' ')) });
      continue;
    }
    const previous = blocks.at(-1);
    if (!paragraph.length && previous?.kind === 'listItem' && line.startsWith('  ')) {
      blocks[blocks.length - 1] = { ...previous, inlines: [...previous.inlines, { kind: 'text', text: ' ' }, ...parseInlines(trimmed)] };
      continue;
    }
    paragraph.push(trimmed);
  }
  flush();
  return blocks;
}

export function parseInlines(text: string): MarkdownInline[] {
  const runs: MarkdownInline[] = [];
  let plain = '';
  const flush = () => { if (plain) { runs.push({ kind: 'text', text: plain }); plain = ''; } };
  let position = 0;
  while (position < text.length) {
    const character = text[position];
    if (character === '`') {
      let ticks = 0;
      while (text[position + ticks] === '`') ticks++;
      const close = text.indexOf('`'.repeat(ticks), position + ticks);
      if (close > position) {
        flush();
        runs.push({ kind: 'code', text: text.slice(position + ticks, close).trim() });
        position = close + ticks;
        continue;
      }
    } else if (character === '[') {
      const link = linkPattern.exec(text.slice(position));
      if (link) {
        flush();
        runs.push({ kind: 'link', text: link[1], url: link[2] });
        position += link[0].length;
        continue;
      }
    } else {
      const emphasis = findEmphasis(text, position);
      if (emphasis) {
        flush();
        runs.push({ kind: emphasis.kind, text: text.slice(position + emphasis.width, emphasis.close) });
        position = emphasis.close + emphasis.width;
        continue;
      }
    }
    plain += character;
    position++;
  }
  flush();
  return runs;
}

function findEmphasis(text: string, position: number): { kind: 'bold' | 'italic'; width: number; close: number } | null {
  const markers: [string, 'bold' | 'italic'][] = [['**', 'bold'], ['__', 'bold'], ['*', 'italic'], ['_', 'italic']];
  for (const [marker, kind] of markers) {
    if (marker[0] === '_' && !isWordBoundary(text, position)) continue;
    const close = emphasisClose(text, position, marker);
    if (close !== null) return { kind, width: marker.length, close };
  }
  return null;
}

function emphasisClose(text: string, position: number, marker: string): number | null {
  if (!text.startsWith(marker, position)) return null;
  const start = position + marker.length;
  if (start >= text.length || /\s/.test(text[start]) || text[start] === marker[0]) return null;
  let close = text.indexOf(marker, start);
  while (close > start && /\s/.test(text[close - 1])) close = text.indexOf(marker, close + marker.length);
  if (close <= start) return null;
  // Underscore emphasis has to close at a word boundary, so snake_case names stay intact.
  if (marker[0] === '_' && close + marker.length < text.length && /[\p{L}\p{N}]/u.test(text[close + marker.length])) return null;
  return close;
}

function isWordBoundary(text: string, position: number) {
  return position === 0 || !/[\p{L}\p{N}]/u.test(text[position - 1]);
}
