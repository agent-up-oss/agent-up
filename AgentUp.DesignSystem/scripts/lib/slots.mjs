/**
 * Component content parameterisation: a text slot and an optional part.
 *
 * States move a modifier class. This covers the other axis a screen legitimately varies about
 * one component - a piece of copy, and whether an optional control is there. The mobile top bar
 * was six components because its title and its leading control differ per page; the real client
 * has one `AppNavBar` taking a title, and this is that shape in the catalog.
 *
 * The declaration lives on the catalog article, naming the parts a plain catalog render keeps:
 *
 *   data-au-parts="menu reload"
 *
 * and on the markup:
 *
 *   <span data-au-part="menu">…</span>        optional, dropped unless the render keeps it
 *   <strong data-au-text="title">Harbor Shop</strong>
 *
 * The screen sets it on the region:
 *
 *   data-au-props="title=Git; parts=menu"
 *
 * `parts` replaces the default set outright, so a screen states the bar it wants rather than a
 * diff against another screen. Unlike a state, this rewrites text, so the screenshot contract
 * cannot re-derive it from the catalog: the generator records the resolved fragment for the
 * scene instead, and the contract compares against that.
 */

const partAttribute = /\sdata-au-part="[^"]*"/;

export function parsePartDeclarations(raw) {
  return new Set((raw ?? '').split(/\s+/).filter(Boolean));
}

export function parseProps(raw) {
  const props = new Map();
  for (const entry of (raw ?? '').split(';').map(value => value.trim()).filter(Boolean)) {
    const split = entry.indexOf('=');
    if (split < 1) throw new Error(`Prop '${entry}' is not '<name>=<value>'.`);
    const name = entry.slice(0, split).trim();
    const value = entry.slice(split + 1).trim();
    if (!name) throw new Error(`Prop '${entry}' has no name.`);
    if (props.has(name)) throw new Error(`Prop '${name}' is set twice.`);
    props.set(name, value);
  }
  return props;
}

/** What a plain catalog render shows: the declared default parts, markers stripped. */
export function applyDefaultParts(component) {
  return resolve(component, parsePartDeclarations(component.parts), new Map());
}

export function applyProps(component, rawProps) {
  const props = parseProps(rawProps);
  if (!props.size) return applyDefaultParts(component);
  // The authored markup, not the default render: that one has had its markers removed.
  const authored = { ...component, html: component.source ?? component.html };
  const available = availableParts(authored.html);
  const keep = props.has('parts')
    ? new Set(props.get('parts').split(/\s+/).filter(Boolean))
    : parsePartDeclarations(component.parts);
  for (const part of keep) {
    if (!available.has(part)) {
      throw new Error(`${component.id} has no part '${part}'. It defines: ${[...available].join(', ') || 'none'}.`);
    }
  }
  const texts = new Map([...props].filter(([name]) => name !== 'parts'));
  return resolve(authored, keep, texts);
}

export function hasContentSlots(component) {
  const html = component.source ?? component.html ?? '';
  return Boolean(component.parts) || /\sdata-au-text="/.test(html);
}

function resolve(component, keep, texts) {
  let html = dropParts(component.html ?? '', keep);
  for (const [name, value] of texts) html = applyText(component.id, html, name, value);
  // Any slot the screen did not set keeps the catalog's own copy; the marker still goes.
  return html.replace(/\sdata-au-text="[^"]*"/g, '');
}

function availableParts(html) {
  return new Set([...html.matchAll(/\sdata-au-part="([^"]+)"/g)].map(match => match[1]));
}

/**
 * Elements are found and rewritten one at a time rather than with a global replace, because
 * removing one shifts the offset of every later one.
 */
function dropParts(html, keep) {
  let current = html;
  for (;;) {
    const element = nextPart(current);
    if (!element) return current;
    if (keep.has(element.part)) {
      const attributes = element.attributes.replace(partAttribute, '');
      current = `${current.slice(0, element.start)}<${element.tag}${attributes}>${current.slice(element.openEnd)}`;
      continue;
    }
    current = trimEmptyLine(`${current.slice(0, element.start)}${current.slice(element.end)}`, element.start);
  }
}

function nextPart(html) {
  const marker = 'data-au-part="';
  let searchFrom = 0;
  while (searchFrom < html.length) {
    const markerAt = html.indexOf(marker, searchFrom);
    if (markerAt < 0) return null;
    if (markerAt === 0 || !isWhitespace(html[markerAt - 1])) {
      searchFrom = markerAt + marker.length;
      continue;
    }
    const start = html.lastIndexOf('<', markerAt);
    if (start < 0) {
      searchFrom = markerAt + marker.length;
      continue;
    }
    const tag = readTagName(html, start + 1);
    const openEnd = tag ? findTagEnd(html, start + 1 + tag.length) : -1;
    if (!tag || openEnd < 0 || markerAt > openEnd) {
      searchFrom = markerAt + marker.length;
      continue;
    }
    const partStart = markerAt + marker.length;
    const partEnd = html.indexOf('"', partStart);
    if (partEnd < 0 || partEnd > openEnd) {
      searchFrom = markerAt + marker.length;
      continue;
    }
    const part = html.slice(partStart, partEnd);
    const closeAt = matchingClose(html, tag, openEnd + 1);
    if (closeAt < 0) throw new Error(`Unclosed data-au-part '${part}'.`);
    return {
      tag,
      attributes: html.slice(start + 1 + tag.length, openEnd),
      part,
      start,
      openEnd: openEnd + 1,
      end: closeAt + `</${tag}>`.length,
    };
  }
  return null;
}

function matchingClose(html, tag, from) {
  const close = `</${tag}>`;
  let depth = 1;
  let cursor = from;
  while (cursor < html.length && depth > 0) {
    const nextOpen = findOpenTag(html, tag, cursor);
    const nextClose = html.indexOf(close, cursor);
    if (nextClose < 0) return -1;
    if (nextOpen >= 0 && nextOpen < nextClose) {
      const end = findTagEnd(html, nextOpen + 1 + tag.length);
      if (end < 0) return -1;
      depth += 1;
      cursor = end + 1;
      continue;
    }
    depth -= 1;
    if (depth === 0) return nextClose;
    cursor = nextClose + close.length;
  }
  return -1;
}

function readTagName(html, from) {
  if (from >= html.length || !isTagStart(html[from])) return '';
  let end = from + 1;
  while (end < html.length && isTagContinue(html[end])) end += 1;
  return html.slice(from, end);
}

function findOpenTag(html, tag, from) {
  const needle = `<${tag}`;
  let at = html.indexOf(needle, from);
  while (at >= 0) {
    const after = at + needle.length;
    if (after >= html.length) return -1;
    const next = html[after];
    if (next === '>' || next === '/' || isWhitespace(next)) return at;
    at = html.indexOf(needle, at + 1);
  }
  return -1;
}

function findTagEnd(html, from) {
  let quote = '';
  for (let i = from; i < html.length; i += 1) {
    const ch = html[i];
    if (quote) {
      if (ch === quote) quote = '';
      continue;
    }
    if (ch === '"' || ch === "'") {
      quote = ch;
      continue;
    }
    if (ch === '>') return i;
  }
  return -1;
}

function isTagStart(ch) {
  return ch >= 'a' && ch <= 'z';
}

function isTagContinue(ch) {
  return isTagStart(ch) || (ch >= '0' && ch <= '9');
}

function isWhitespace(ch) {
  return ch === ' ' || ch === '\n' || ch === '\r' || ch === '\t';
}

/** Removing an element that sat on its own line otherwise leaves its indentation behind. */
function trimEmptyLine(html, at) {
  const lineStart = html.lastIndexOf('\n', at - 1) + 1;
  const lineEnd = html.indexOf('\n', at);
  if (lineEnd < 0) return html;
  if (html.slice(lineStart, lineEnd).trim() !== '') return html;
  return `${html.slice(0, lineStart)}${html.slice(lineEnd + 1)}`;
}

function applyText(id, html, name, value) {
  const pattern = new RegExp(
    `(<([a-z][a-z0-9]*)((?:[^>"']|"[^"]*"|'[^']*')*)\\sdata-au-text="${name}"((?:[^>"']|"[^"]*"|'[^']*')*)>)([\\s\\S]*?)(</\\2>)`,
    'i');
  const match = html.match(pattern);
  if (!match) throw new Error(`${id} has no text slot '${name}'.`);
  return html.replace(pattern, `<${match[2]}${match[3]}${match[4]}>${escapeText(value)}${match[6]}`);
}

function escapeText(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

/**
 * The documented examples of a component's content variations, generated from its own markup.
 *
 * Six mobile bars existed so the catalog could show a reader what the Git bar and the back bar
 * say. Declaring those examples keeps every one of them on the page while the markup is
 * authored once.
 *
 *   data-au-props-examples="title=Git; parts=menu title|Git top bar|Inner Git pages keep it."
 *
 * Entries are separated by `;` inside the props and `|` between props, title and note, so the
 * props themselves use `,`-free `name=value` pairs and the parser splits on the last two bars.
 */
export function parsePropsExamples(raw) {
  const examples = [];
  for (const entry of (raw ?? '').split(';;').map(value => value.trim()).filter(Boolean)) {
    const parts = entry.split('|').map(value => value.trim());
    if (parts.length !== 3 || parts.some(value => !value)) {
      throw new Error(`Props example '${entry}' is not '<props>|<title>|<note>'.`);
    }
    examples.push({ props: parts[0], title: parts[1], note: parts[2] });
  }
  return examples;
}

export function buildPropsExamples(component) {
  return parsePropsExamples(component.propsExamples).map(example => ({
    props: example.props,
    title: example.title,
    note: example.note,
    html: applyProps(component, example.props),
  }));
}
