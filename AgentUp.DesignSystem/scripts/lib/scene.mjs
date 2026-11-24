/**
 * The New UI scene is the page.
 *
 * `assembleScreens` already turns a scene into one component tree for the web showcase.
 * This walks that same assembly — layout slots, then `data-au-use` — and emits the holes
 * (slot, state, action) plus the Avalonia and React Native scene that mount the tree.
 * Clients bind Server state into the holes. They do not draw the page again.
 */

import { parseSelector, resolveVars, toPx } from './new-ui/css.mjs';
import { pathData } from './icons.mjs';
import { parseScreens, slottedScene } from './screens.mjs';
import { parsePartDeclarations, parseProps } from './slots.mjs';
import { applyStates } from './states.mjs';

const voidTags = new Set(['area', 'base', 'br', 'col', 'embed', 'hr', 'img', 'input', 'link', 'meta', 'source', 'track', 'wbr']);
const skipTags = new Set(['svg', 'style', 'script']);

/** Classes whose presence means the scene already owns that region of the page. */
export const sceneRegionClasses = [
  'au-screen', 'au-screen-body', 'au-screen-rail', 'au-screen-main', 'au-screen-tabs',
  'au-screen-subtabs', 'au-screen-toolbar', 'au-screen-pane', 'au-screen-aside',
  'aun-phone', 'aun-phone__head', 'aun-phone__body', 'aun-phone__dock',
  'aun-state-map', 'aun-task-board', 'aun-validate', 'aun-settings',
  'au-validation-sidebar', 'au-mobile-tab-bar',
];

const columnFill = new Set(['au-screen', 'aun-phone', 'au-screen-main', 'au-screen-pane', 'au-screen-rail']);
const gridBody = new Set(['au-screen-body']);
const chromeLayout = new Set(['au-chrome']);
const rowLayout = new Set([
  'au-screen-tabs', 'au-screen-subtabs', 'au-screen-toolbar', 'au-mobile-tab-bar', 'au-tabs',
  'aun-agent-head', 'aun-phone__head-row', 'au-validation-dock',
  'au-cluster', 'au-logo-lockup', 'au-workspace', 'au-workspace-head', 'au-badge',
  'aun-state-map__root',
]);
const spreadLayout = new Set(['aun-nav-row', 'aun-state-map__head', 'aun-settings__row', 'aun-session']);
const trioLayout = new Set(['aun-state-map__branch-head', 'aun-task-row', 'au-validation-flow-head']);
const pairLayout = new Set(['au-validation-step', 'au-validation-check']);
const columnLayout = new Set([
  'aun-rail__nav', 'au-screen-aside', 'aun-phone__body', 'aun-phone__head',
  'aun-settings', 'aun-settings__list', 'aun-task-board', 'aun-state-map', 'aun-validate', 'au-validation-sidebar',
  'aun-state-map__lane', 'aun-state-map__branch', 'aun-state-map__branch-copy', 'aun-task-row__body',
]);

/**
 * One scene per entry: the tree both clients mount, and the holes they are allowed to fill.
 * `components` is a Map of catalog id to the component record. New UI records are the
 * pre-demo markup so a demo fill stays a showcase and the holes stay open.
 */
export function sceneBundle(screensHtml, components, rules = [], tokens = {}) {
  const { layouts, scenes } = parseScreens(screensHtml);
  const templates = {};
  const built = scenes.map(scene => {
    const tree = expand(parseNodes(slottedScene(scene, layouts)), components, 0);
    return {
      id: scene.id,
      surface: scene.platform,
      tree,
      requests: scene.regions.map(region => region.state).filter(Boolean),
    };
  });
  for (const scene of built) collectTemplates(scene.tree, components, templates);
  for (const scene of built) tune(scene.tree, false);
  for (const template of Object.values(templates)) tune([template], false);
  const facts = flexFacts(rules, tokens);
  for (const scene of built) applyFlow(scene.tree, facts);
  for (const template of Object.values(templates)) applyFlow([template], facts);
  const contracts = built.map(scene => sceneContract(scene, templates));
  const regions = sceneRegionClasses.filter(name => built.some(scene => hasClass(scene.tree, name)));
  return { scenes: built, templates, contracts, regions };
}

export function sceneContract(scene, templates = {}) {
  const slots = new Map();
  const states = new Set();
  const actions = new Set();
  const visit = (node, insideRepeat, seen = new Set()) => {
    recordNode(node, slots, states, actions, insideRepeat);
    // A content slot's default is sample copy, except when it declares states of its own (the
    // dock): then it is a working default and publishes its holes, which a client that fills the
    // slot simply never binds.
    const workingDefault = Boolean(node.content) && (node.flags ?? []).length > 0;
    const nested = insideRepeat || Boolean(node.repeat) || (Boolean(node.content) && !workingDefault);
    for (const child of node.k ?? []) visit(child, nested, seen);
    if (node.repeat && templates[node.repeat] && !seen.has(node.repeat)) {
      const next = new Set(seen);
      next.add(node.repeat);
      visit(templates[node.repeat], true, next);
    }
  };
  for (const node of scene.tree) visit(node, false);
  for (const request of scene.requests ?? []) {
    for (const entry of request.split(/\s+/).filter(Boolean)) {
      const name = entry.includes(':') ? entry.slice(0, entry.indexOf(':')) : entry;
      if (name) states.add(name);
    }
  }
  return {
    id: scene.id,
    surface: scene.surface,
    slots: [...slots.values()],
    states: [...states].sort(),
    actions: [...actions].sort(),
  };
}

function recordNode(node, slots, states, actions, insideRepeat) {
  let name = null;
  if (!insideRepeat) {
    if (node.slot) name = node.slot;
    else if (node.repeat) name = node.demo || node.repeat;
    else if (node.demo && !(node.k ?? []).length) name = node.demo;
    else if (node.text) name = node.text;
  }
  if (name && !slots.has(name)) {
    slots.set(name, {
      name,
      kind: node.repeat ? 'repeat' : node.content ? 'content' : 'text',
      ...(node.repeat ? { repeat: node.repeat } : {}),
    });
  }
  if (node.action) actions.add(node.action);
  for (const action of node.actions ?? []) actions.add(action);
  if (node.selection) states.add(node.selection[0]);
  for (const flag of node.flags ?? []) states.add(flag[0]);
  if (node.when) states.add(node.when);
}

function collectTemplates(nodes, components, templates) {
  const visit = node => {
    if (node.repeat && !templates[node.repeat]) {
      const component = components.get(node.repeat);
      if (!component) throw new Error(`Scene repeats unknown component '${node.repeat}'.`);
      templates[node.repeat] = { t: 'div', k: expand(instantiate(component, '', '', true), components, 0) };
      collectTemplates(templates[node.repeat].k, components, templates);
    }
    for (const child of node.k ?? []) visit(child);
  };
  for (const node of nodes) visit(node);
}

function expand(nodes, components, depth) {
  if (depth > 64) throw new Error('Assembled screen uses nested too deeply.');
  const out = [];
  for (const node of nodes) {
    if (!node.use) {
      if (node.k) node.k = expand(node.k, components, depth + 1);
      out.push(node);
      continue;
    }
    const component = components.get(node.use);
    if (!component) throw new Error(`Assembled screen references unknown catalog component '${node.use}'.`);
    const instance = expand(instantiate(component, node.props, node.state), components, depth + 1);
    if (node.when && instance.length === 1) instance[0].when = node.when;
    const inner = node.k?.length ? expand(node.k, components, depth + 1) : [];
    if (inner.length && instance.length === 1) {
      instance[0].k = [...(instance[0].k ?? []), ...inner];
    }
    out.push(...instance);
  }
  return out;
}

function instantiate(component, propsRaw, state, keepAllParts = false) {
  const source = component.source ?? component.html;
  const html = state ? applyStates({ ...component, html: source }, state) : source;
  const nodes = parseNodes(html);
  const props = propsRaw ? parseProps(propsRaw) : new Map();
  const keep = keepAllParts ? null
    : props.has('parts')
      ? new Set(props.get('parts').split(/\s+/).filter(Boolean))
      : (component.parts != null ? parsePartDeclarations(component.parts) : null);
  if (keep) pruneParts(nodes, keep);
  for (const [name, value] of props) {
    if (name !== 'parts') writeText(nodes, name, value);
  }
  return nodes;
}

function pruneParts(nodes, keep) {
  for (const node of nodes) {
    if (!node.k) continue;
    node.k = node.k.filter(child => !child.part || keep.has(child.part));
    pruneParts(node.k, keep);
  }
}

function writeText(nodes, name, value) {
  for (const node of nodes) {
    if (node.text === name) {
      node.x = value;
      node.k = (node.k ?? []).filter(child => child.t !== '#text');
    }
    if (node.k) writeText(node.k, name, value);
  }
}

function hasClass(nodes, name) {
  for (const node of nodes) {
    if (node.c?.includes(name)) return true;
    if (node.k && hasClass(node.k, name)) return true;
  }
  return false;
}

export function parseNodes(html) {
  const nodes = [];
  let index = 0;
  while (index < html.length) {
    if (html.startsWith('<!--', index)) {
      const end = html.indexOf('-->', index);
      index = end < 0 ? html.length : end + 3;
      continue;
    }
    if (html[index] !== '<') {
      const next = html.indexOf('<', index);
      const raw = html.slice(index, next < 0 ? html.length : next);
      const text = decode(raw).replace(/\s+/g, ' ').trim();
      if (text) nodes.push({ t: '#text', x: text });
      index = next < 0 ? html.length : next;
      continue;
    }
    const tag = readTag(html, index);
    if (!tag) break;
    if (tag.closing) {
      index = tag.end;
      continue;
    }
    const node = nodeFrom(tag);
    index = tag.end;
    if (skipTags.has(tag.name)) {
      if (!tag.selfClosing) {
        const close = `</${tag.name}>`;
        const innerEnd = findClose(html, tag.name, index);
        if (tag.name === 'svg' && innerEnd >= 0) {
          // Circles and rects are drawn too: a scene icon is the catalog icon, not its paths alone.
          const geometry = html.slice(index, innerEnd);
          if (/<(path|rect|circle)\s/.test(geometry)) node.d = pathData('scene icon', geometry);
        }
        index = innerEnd < 0 ? html.length : innerEnd + close.length;
      }
      nodes.push(node);
      continue;
    }
    if (tag.selfClosing || voidTags.has(tag.name)) {
      nodes.push(node);
      continue;
    }
    const close = `</${tag.name}>`;
    const innerEnd = findClose(html, tag.name, index);
    if (innerEnd < 0) throw new Error(`Unclosed <${tag.name}>.`);
    node.k = parseNodes(html.slice(index, innerEnd));
    if (!node.k.length) delete node.k;
    nodes.push(node);
    index = innerEnd + close.length;
  }
  return nodes;
}

function nodeFrom(tag) {
  const node = { t: tag.name };
  const classes = attr(tag.attrs, 'class');
  if (classes) node.c = classes.split(/\s+/).filter(Boolean);
  const use = data(tag.attrs, 'use');
  if (use) node.use = use;
  const props = data(tag.attrs, 'props');
  if (props) node.props = props;
  const state = data(tag.attrs, 'state');
  if (state) node.state = state;
  const slot = data(tag.attrs, 'slot');
  if (slot) {
    node.slot = slot;
    node.content = true;
  }
  const text = data(tag.attrs, 'text');
  if (text) node.text = text;
  const part = data(tag.attrs, 'part');
  if (part) node.part = part;
  const action = data(tag.attrs, 'action');
  if (action) node.action = action;
  const label = attr(tag.attrs, 'aria-label');
  if (label) node.label = decode(label);
  const id = data(tag.attrs, 'id');
  if (id) node.id = id;
  const actions = data(tag.attrs, 'actions');
  if (actions) node.actions = actions.split(/\s+/).filter(Boolean);
  const repeat = data(tag.attrs, 'repeat');
  if (repeat) node.repeat = repeat;
  const demo = data(tag.attrs, 'demo');
  if (demo) node.demo = demo;
  const when = data(tag.attrs, 'when');
  if (when) node.when = when;
  // Presentation rules a client must not restate: which rows group under which label, the
  // tone and copy a button takes for its row's action, which parts belong to which action,
  // and what an empty slot says.
  const groups = data(tag.attrs, 'groups');
  if (groups) node.groups = actionMap(groups);
  const tone = data(tag.attrs, 'tone');
  if (tone) node.tone = Object.fromEntries(actionMap(tone));
  const labels = data(tag.attrs, 'labels');
  if (labels) node.labels = Object.fromEntries(actionMap(labels));
  const forActions = data(tag.attrs, 'for');
  if (forActions) node.for = forActions.split(/\s+/).filter(Boolean);
  const empty = data(tag.attrs, 'empty');
  if (empty) node.empty = empty;
  const selection = data(tag.attrs, 'selection');
  if (selection) {
    const [group, index, modifier] = selection.split(':');
    node.selection = [group, index, modifier];
  }
  const flags = data(tag.attrs, 'flags');
  if (flags) node.flags = flags.split(/\s+/).filter(Boolean).map(entry => entry.split(':'));
  if (tag.name === 'input') {
    const value = attr(tag.attrs, 'value');
    if (value) node.x = decode(value);
    const placeholder = attr(tag.attrs, 'placeholder');
    if (placeholder) node.placeholder = decode(placeholder);
    if ((attr(tag.attrs, 'type') || '') === 'password') node.secret = true;
  }
  if (data(tag.attrs, 'kind') === '1') node.kindHost = true;
  const layout = layoutOf(node.c ?? []);
  if (layout) node.layout = layout;
  return node;
}

/** `action:value; action:value` as ordered pairs. Values may hold spaces; `;` separates entries. */
function actionMap(raw) {
  return decode(raw).split(';').map(entry => entry.trim()).filter(Boolean).map(entry => {
    const at = entry.indexOf(':');
    return [entry.slice(0, at).trim(), entry.slice(at + 1).trim()];
  });
}

function layoutOf(classes) {
  if (classes.some(name => chromeLayout.has(name))) return 'chrome';
  if (classes.some(name => gridBody.has(name))) return 'grid';
  if (classes.some(name => columnFill.has(name))) return 'fill';
  if (classes.some(name => spreadLayout.has(name))) return 'spread';
  if (classes.some(name => trioLayout.has(name))) return 'trio';
  if (classes.some(name => pairLayout.has(name))) return 'pair';
  if (classes.some(name => rowLayout.has(name))) return 'row';
  if (classes.some(name => columnLayout.has(name))) return 'column';
  return '';
}

/**
 * The scene mounts the same CSS the catalog does. Flex, alignment, gap, and an auto margin are
 * the layout; the hardcoded kinds above only name regions the page shell already treats specially.
 */
function flexFacts(rules, tokens) {
  const facts = new Map();
  for (const rule of rules) {
    for (const selector of rule.selectors) {
      const parsed = parseSelector(selector);
      if (!parsed || parsed.pseudo || parsed.classes.length !== 1) continue;
      const fact = facts.get(parsed.classes[0]) ?? {};
      for (const [property, raw] of rule.declarations) {
        const value = resolveVars(raw, tokens);
        if (property === 'display' && (value === 'flex' || value === 'inline-flex')) fact.display = 'flex';
        if (property === 'flex-direction' && (value === 'row' || value === 'column')) fact.dir = value;
        if (property === 'align-items' && (value === 'center' || value === 'flex-start' || value === 'start' || value === 'stretch')) fact.align = value === 'flex-start' || value === 'start' ? 'start' : value;
        if (property === 'justify-content' && (value === 'space-between' || value === 'center' || value === 'space-around')) fact.justify = value === 'space-between' ? 'between' : value === 'space-around' ? 'around' : 'center';
        if ((property === 'flex' && /^1(\s|$)/.test(value.trim())) || (property === 'flex-grow' && value.trim() === '1')) fact.grow = true;
        if (property === 'white-space' && value.trim() === 'nowrap') fact.nowrap = true;
        if (property === 'flex-wrap' && value.trim() === 'wrap') fact.wrap = true;
        if (property === 'gap') {
          const gap = toPx(value.split(/\s+/)[0]);
          if (gap != null) fact.gap = gap;
        }
        if ((property === 'margin-left' || property === 'margin-inline-start') && value.trim() === 'auto') fact.push = true;
      }
      facts.set(parsed.classes[0], fact);
    }
  }
  return facts;
}

function applyFlow(nodes, facts) {
  for (const node of nodes ?? []) {
    const flow = {};
    let push = false;
    for (const name of node.c ?? []) {
      const fact = facts.get(name);
      if (!fact) continue;
      if (fact.display) flow.display = fact.display;
      if (fact.dir) flow.dir = fact.dir;
      if (fact.align) flow.align = fact.align;
      if (fact.justify) flow.justify = fact.justify;
      if (fact.gap != null) flow.gap = fact.gap;
      if (fact.wrap) flow.wrap = true;
      if (fact.push) push = true;
      if (fact.grow) node.grow = true;
      if (fact.nowrap) node.nowrap = true;
    }
    if (push) node.push = true;
    if (flow.display) {
      node.flow = { d: flow.dir ?? 'row' };
      if (flow.align) node.flow.a = flow.align;
      if (flow.justify) node.flow.j = flow.justify;
      if (flow.gap != null) node.flow.g = flow.gap;
      if (flow.wrap) node.flow.w = 'wrap';
    }
    if (node.k) applyFlow(node.k, facts);
  }
}

/** The phone stacks fleet lanes. A text slot keeps its default as `x` and drops the sample text node. */
function tune(nodes, mobile) {
  for (const node of nodes ?? []) {
    const classes = node.c ?? [];
    const here = mobile || classes.includes('aun-state-map--mobile');
    if (classes.includes('aun-state-map__lanes')) node.layout = here ? 'column' : 'lanes';
    if (node.text && node.k) {
      const defaults = node.k.filter(child => child.t === '#text').map(child => child.x).filter(Boolean);
      node.k = node.k.filter(child => child.t !== '#text');
      if (!node.k.length) delete node.k;
      if (!node.x && defaults.length) node.x = defaults.join(' ');
    }
    if (node.k) tune(node.k, here);
  }
}

function readTag(html, index) {
  if (html[index] !== '<') return null;
  let cursor = index + 1;
  const closing = html[cursor] === '/';
  if (closing) cursor += 1;
  const name = readName(html, cursor);
  if (!name) return null;
  cursor += name.length;
  let quote = '';
  const attrStart = cursor;
  for (; cursor < html.length; cursor += 1) {
    const ch = html[cursor];
    if (quote) {
      if (ch === quote) quote = '';
      continue;
    }
    if (ch === '"' || ch === "'") {
      quote = ch;
      continue;
    }
    if (ch === '>') break;
  }
  if (html[cursor] !== '>') return null;
  const raw = html.slice(attrStart, cursor);
  const selfClosing = /\/\s*$/.test(raw);
  return { name, closing, selfClosing, attrs: raw, end: cursor + 1 };
}

function readName(html, index) {
  if (!/[A-Za-z]/.test(html[index] ?? '')) return '';
  let end = index + 1;
  while (end < html.length && /[A-Za-z0-9-]/.test(html[end])) end += 1;
  return html.slice(index, end);
}

function findClose(html, tag, from) {
  const open = `<${tag}`;
  const close = `</${tag}>`;
  let depth = 1;
  let cursor = from;
  while (cursor < html.length && depth > 0) {
    const nextOpen = findOpen(html, open, tag, cursor);
    const nextClose = html.indexOf(close, cursor);
    if (nextClose < 0) return -1;
    if (nextOpen >= 0 && nextOpen < nextClose) {
      const tagEnd = readTag(html, nextOpen);
      depth += 1;
      cursor = tagEnd ? tagEnd.end : nextOpen + open.length;
      continue;
    }
    depth -= 1;
    if (depth === 0) return nextClose;
    cursor = nextClose + close.length;
  }
  return -1;
}

function findOpen(html, needle, tag, from) {
  let at = html.indexOf(needle, from);
  while (at >= 0) {
    const after = html[at + needle.length];
    if (after === '>' || after === '/' || after === ' ' || after === '\n' || after === '\t') return at;
    at = html.indexOf(needle, at + 1);
  }
  return -1;
}

function attr(raw, name) {
  const match = raw.match(new RegExp(`(?:^|\\s)${name}\\s*=\\s*"([^"]*)"`));
  return match ? match[1] : '';
}

function data(raw, name) {
  return attr(raw, `data-au-${name}`);
}

function decode(value) {
  return value
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>')
    .replaceAll('&quot;', '"')
    .replaceAll('&#39;', "'")
    .replaceAll('&amp;', '&');
}

export function emitSceneNative(bundle) {
  const payload = JSON.stringify({
    scenes: Object.fromEntries(bundle.scenes.map(scene => [scene.id, scene.tree])),
    templates: bundle.templates,
    contracts: bundle.contracts,
  });
  return `// Generated from src/new-ui by scripts/build.mjs. Do not edit.
import { newUiTheme, newUiBox, newUiText } from './new-ui.js';

export const newUiSceneDocument = ${payload};

const bindingName = (className) => {
  const camel = (name) => name.replace(/-([a-z0-9])/g, (_, character) => character.toUpperCase());
  if (className.startsWith('aun-')) return camel(className.replaceAll('__', '-').replaceAll('--', '-'));
  return camel(className.replace(/^au-/, '').replaceAll('--', '-'));
};

function paint(classes, kind, scope = []) {
  const own = (classes ?? []).map(bindingName);
  const names = own.filter(name => newUiTheme.components[name]);
  // \`.branch--waiting .mark\`: an ancestor's class can paint a descendant.
  for (const outer of scope) {
    for (const name of own) {
      if (newUiTheme.components[\`\${outer}__\${name}\`]) names.push(\`\${outer}__\${name}\`);
    }
  }
  if (!names.length) return undefined;
  return kind === 'text' ? newUiText(...names) : newUiBox(...names);
}

/** A selection names a fixed index, or \`@id\`: the row whose id the state names is the selected one. */
function selected(node, model, item) {
  if (!node.selection) return false;
  const target = node.selection[1] === '@id' ? item?.id : node.selection[1];
  return target != null && String(model.state[node.selection[0]] ?? '') === String(target);
}

function flagged(node, model, item) {
  const extra = [];
  for (const [name, modifier] of node.flags ?? []) {
    if (model.state[name]) extra.push(modifier);
  }
  if (selected(node, model, item)) extra.push(node.selection[2]);
  return extra;
}

const inlineTags = new Set(['button', 'a', 'span', 'label', 'strong', 'em', 'code']);

/**
 * Rows in the order the scene declares their groups, the first row of each group carrying the
 * group's label and count as its section. Rows whose action names no group follow, unlabelled.
 */
function grouped(groups, items) {
  if (!groups) return items;
  const out = [];
  for (const [action, label] of groups) {
    const rows = items.filter(row => row.action === action);
    rows.forEach((row, index) => out.push(index === 0
      ? Object.assign({}, row, { parts: ['section'].concat(row.parts || []), text: Object.assign({}, row.text, { section: label + ' · ' + rows.length }) })
      : row));
  }
  const named = new Set(groups.map(entry => entry[0]));
  return out.concat(items.filter(row => !named.has(row.action)));
}

function holdsSlot(node) {
  if (!node) return false;
  if (node.content && !node.repeat) return true;
  return (node.k ?? []).some(holdsSlot);
}

/**
 * \`native\` is { View, Text, Pressable, ScrollView, Icon? }. \`Icon\` receives { paths, color, size }
 * with 24-unit path data; without it an icon keeps its box and draws nothing.
 */
export function createNewUiScene(React, native) {
  const { View, Text, Pressable, ScrollView, Icon, TextInput } = native;
  const drafts = {};
  const bound = {};
  const h = React.createElement;
  // The page sets the ink on the web (.aun sets color); a React Native Text has none of its own.
  const root = { scope: [], ink: { color: newUiTheme.colors.textPrimary } };
  function render(node, model, item, key, ctx = root) {
    if (!node || node.t === 'style' || node.t === 'script' || node.t === 'path' || node.t === 'circle') return null;
    if (node.t === 'svg') {
      if (!node.d) return null;
      if (!Icon) return h(View, { key, style: { width: 20, height: 20 } });
      return h(Icon, { key, paths: node.d, color: ctx.ink?.color, size: 20 });
    }
    if (node.t === '#text') return h(Text, { key, style: ctx.ink }, node.x);
    if (node.when && !model.state[node.when]) return null;
    if (node.empty && ((model.items[node.empty] || []).length > 0)) return null;
    if (node.for && !(item && node.for.includes(item.action))) return null;
    if (node.part && item?.parts && !item.parts.includes(node.part)) return null;
    const slotName = node.slot || node.demo;
    if (node.repeat) {
      const items = grouped(node.groups, (item?.items && item.items[slotName]) || model.items[slotName] || []);
      const template = newUiSceneDocument.templates[node.repeat];
      const inner = { scope: [...ctx.scope, ...(node.c ?? []).map(bindingName)], ink: paint(node.c, 'text', ctx.scope) ?? ctx.ink };
      const rows = items.map((child, index) => {
        const row = render(template, model, child, String(child.id ?? index), inner);
        if (!node.action || child.action) return row;
        return h(Pressable, {
          key: String(child.id ?? index),
          accessibilityRole: 'button',
          onPress: () => model.onAction(node.action, child.id ?? null),
          style: { alignSelf: 'stretch' },
        }, row);
      });
      return h(View, { key, style: [paint(node.c, 'box', ctx.scope), layoutStyle(node)] }, rows);
    }
    if (node.content && model.content && model.content[node.slot] != null) return model.content[node.slot];
    const classes = [...(node.c ?? []), ...flagged(node, model, item)];
    if (node.tone && item?.action && node.tone[item.action]) classes.push(node.tone[item.action]);
    // A row's state names its host; a status dot inside it takes only a dot state.
    const dot = (node.c ?? []).includes('au-status-dot');
    if (item?.state && (node.kindHost || (node.c ?? []).includes('au-workspace') || (dot && item.state.startsWith('au-status-dot')))) classes.push(item.state);
    // The catalog bakes its default selection into the markup; once the state names another
    // member of the group, that baked class has to go.
    if (node.selection && node.selection[1] !== '@id' && model.state[node.selection[0]] != null
      && String(model.state[node.selection[0]]) !== String(node.selection[1])) {
      const baked = classes.indexOf(node.selection[2]);
      if (baked >= 0) classes.splice(baked, 1);
    }
    const box = paint(classes, 'box', ctx.scope);
    const ink = { ...ctx.ink, ...paint(classes, 'text', ctx.scope) };
    if (classes.includes('aun-task__grip')) {
      const bar = { width: 18, height: 2, borderRadius: 1, backgroundColor: newUiTheme.colors.textMuted };
      return h(View, { key, style: { gap: 3 } }, h(View, { style: bar }), h(View, { style: bar }), h(View, { style: bar }));
    }
    if (classes.includes('aun-state-map__drop')) {
      // The stem ends in a dot (\`::after\` on the web).
      return h(View, { key, style: { alignSelf: 'center', alignItems: 'center', height: 20 } },
        h(View, { style: [box, { height: 16 }] }),
        h(View, { style: { width: 8, height: 8, marginTop: -4, borderRadius: 4, backgroundColor: newUiTheme.colors.textFaint } }));
    }
    const inner = { scope: [...ctx.scope, ...classes.map(bindingName)], ink };
    // A block that holds nothing but words is one line of copy, painted by its own class.
    const words = (node.k ?? []).length > 0 && node.k.every(child => child.t === '#text')
      ? node.k.map(child => child.x).join(' ').trim()
      : '';
    const labelled = node.labels && item?.action ? node.labels[item.action] : undefined;
    const textValue = node.text
      ? (item?.text?.[node.text] ?? labelled ?? model.text[node.text] ?? node.x ?? '')
      : (labelled ?? node.x ?? (words || undefined));
    const childNodes = words ? [] : (node.k ?? []);
    const children = childNodes.map((child, index) => render(child, model, item, index, inner)).filter(child => child != null);
    const action = node.action ? (item?.action || node.action) : null;
    if (node.t === 'input') {
      if (!TextInput) return h(View, { key, style: [box, { minHeight: 36 }] });
      const fieldValue = textValue ?? node.x ?? '';
      if (node.action && node.text) {
        if (bound[node.action] !== fieldValue) {
          drafts[node.action] = fieldValue;
          bound[node.action] = fieldValue;
        }
      } else if (node.action && drafts[node.action] === undefined && fieldValue) {
        drafts[node.action] = fieldValue;
      }
      return h(TextInput, {
        key: node.text ? \`\${key}:\${fieldValue}\` : key,
        defaultValue: (node.action ? drafts[node.action] : undefined) ?? fieldValue,
        placeholder: node.placeholder,
        secureTextEntry: node.secret === true,
        onChangeText: (value) => { if (node.action) drafts[node.action] = value; },
        style: [box, ink, { flexGrow: 1, minWidth: 0 }],
      });
    }
    const block = !inlineTags.has(node.t) && (!node.flow || node.flow.d === 'column');
    if (action && block) {
      // A card that opens something is still a card: it stacks and spans its column.
      const body = textValue ? [h(Text, { key: 'label', style: ink }, textValue), ...children] : children;
      return h(Pressable, {
        key,
        accessibilityRole: 'button',
        accessibilityLabel: node.label,
        onPress: () => model.onAction(action, item?.id ?? null, drafts[action], { ...drafts }),
        style: [box, measure(node.c), layoutStyle(node), { alignSelf: 'stretch' }, node.push ? { marginLeft: 'auto' } : null],
      }, body);
    }
    if (node.t === 'button' || action) {
      const column = node.flow?.d === 'column';
      const body = [...children];
      if (textValue && !childNodes.some(child => child.t === '#text')) {
        body.push(h(Text, { key: 'label', style: ink }, String(textValue).trim()));
      }
      const inner = body.length > 1
        ? h(View, { style: { flexDirection: column ? 'column' : 'row', alignItems: 'center', gap: node.flow?.g ?? (column ? 2 : 6) } }, body)
        : body;
      const spoken = node.label || textValue || (node.k ?? []).filter(child => child.t === '#text').map(child => child.x).join(' ').trim();
      // A locked field keeps its control so the reader sees what it would do, and it does nothing.
      const locked = Boolean(node.part && item?.parts?.includes('locked'));
      return h(Pressable, {
        key,
        accessibilityRole: 'button',
        accessibilityLabel: spoken || undefined,
        accessibilityState: locked ? { disabled: true } : undefined,
        disabled: locked,
        onPress: locked ? undefined : () => model.onAction(action, item?.id ?? null, drafts[action], { ...drafts }),
        style: [{ alignItems: 'center', justifyContent: 'center' }, box, node.grow ? { flex: 1 } : null, node.push ? { marginLeft: 'auto' } : null, locked ? { opacity: 0.4 } : null],
      }, inner);
    }
    if (textValue && !childNodes.some(child => child.t !== '#text')) {
      return h(Text, { key, style: [ink, box, node.grow ? { flex: 1, minWidth: 0 } : null], numberOfLines: node.nowrap ? 1 : undefined }, textValue);
    }
    const body = textValue ? [h(Text, { key: 'label', style: ink }, textValue), ...children] : children;
    const grows = (node.c ?? []).some(name => name === 'au-screen-body' || name === 'au-screen-pane' || name === 'aun-phone__body' || name === 'aun-rail__nav' || name === 'aun-application');
    const arranged = arrange(h, View, body, node.layout);
    // Only the phone body scrolls, and only when no client screen fills it: those scroll themselves.
    if (grows && node.layout === 'column' && !holdsSlot(node)) {
      return h(ScrollView, {
        key,
        style: [{ flex: 1 }, box],
        contentContainerStyle: [flowStyle(node), { flexDirection: 'column' }],
      }, arranged);
    }
    return h(View, {
      key,
      style: [box, measure(node.c), layoutStyle(node), grows ? { flex: 1, minHeight: 0 } : null, node.grow ? { flex: 1, minWidth: 0 } : null, node.push ? { marginLeft: 'auto' } : null],
    }, arranged);
  }
  function layoutStyle(node) {
    const layout = node?.layout || (node?.flow?.d === 'row' ? 'row' : '');
    const flow = flowStyle(node);
    if (layout === 'fill' || layout === 'column') return { flex: layout === 'fill' ? 1 : undefined, flexDirection: 'column', ...flow };
    if (layout === 'row' || layout === 'trio') return { flexDirection: 'row', alignItems: 'center', gap: layout === 'trio' ? 8 : undefined, ...flow };
    if (layout === 'spread') return { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', ...flow };
    if (layout === 'chrome') return { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', ...flow };
    if (layout === 'grid' || layout === 'lanes') return { flexDirection: 'row', flex: 1, alignItems: 'stretch', ...flow };
    return { flexDirection: 'column', ...flow };
  }
  function flowStyle(node) {
    const flow = node?.flow;
    if (!flow) return {};
    const style = {};
    if (flow.d === 'column') style.flexDirection = 'column';
    if (flow.d === 'row') style.flexDirection = 'row';
    if (flow.a === 'center') style.alignItems = 'center';
    if (flow.a === 'start') style.alignItems = 'flex-start';
    if (flow.j === 'between') style.justifyContent = 'space-between';
    if (flow.j === 'center') style.justifyContent = 'center';
    if (flow.j === 'around') style.justifyContent = 'space-around';
    if (flow.g != null) style.gap = flow.g;
    if (flow.w === 'wrap') style.flexWrap = 'wrap';
    return style;
  }
  function measure(classes) {
    const names = classes ?? [];
    // The catalog frames a phone at 390 by 844; on a device the scene is the screen.
    if (names.includes('aun-phone')) return { width: '100%', height: '100%' };
    if (names.includes('aun-settings')) return { alignSelf: 'center', width: '100%', maxWidth: 576 };
    if (names.includes('aun-state-map__root')) return { alignSelf: 'center', maxWidth: 256 };
    if (names.includes('aun-state-map__stem') || names.includes('aun-state-map__drop')) return { alignSelf: 'center' };
    return null;
  }
  function arrange(create, Box, children, layout) {
    if (layout === 'grid') {
      return children.map((child, index) => create(Box, {
        key: 'col-' + index,
        style: index === 0 ? { width: 264 } : index === children.length - 1 && children.length > 2 ? null : { flex: 1 },
      }, child));
    }
    if (layout === 'trio') {
      // auto, 1fr, auto: the middle takes what the ends leave.
      return children.map((child, index) => index === 1 && children.length >= 3
        ? create(Box, { key: 'trio-' + index, style: { flex: 1, minWidth: 0 } }, child)
        : child);
    }
    if (layout === 'lanes') {
      return children.map((child, index) => create(Box, { key: 'lane-' + index, style: { flex: 1, minWidth: 0 } }, child));
    }
    return children;
  }
  /**
   * @param {{ scene: string, text?: Record<string, string>, items?: Record<string, unknown[]>, state?: Record<string, string>, content?: Record<string, unknown>, onAction?: (name: string, id: string | null, text?: string, fields?: Record<string, string>) => void }} props
   */
  return function NewUiScene(props) {
    const { scene, text = {}, items = {}, state = {}, content = {}, onAction = () => {} } = props;
    const tree = newUiSceneDocument.scenes[scene];
    if (!tree) return null;
    const model = { text, items, state, content, onAction };
    return h(View, { style: { flex: 1 } }, tree.map((node, index) => render(node, model, null, index)));
  };
}
`;
}

export function emitSceneAvalonia(bundle) {
  const payload = JSON.stringify({
    scenes: Object.fromEntries(bundle.scenes.map(scene => [scene.id, scene.tree])),
    templates: bundle.templates,
  });
  const source = `// <auto-generated />
// Generated from src/new-ui by scripts/build.mjs. Do not edit.
#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace AgentUp.Desktop.DesignSystem;

public sealed class NewUiActionEventArgs : EventArgs
{
    public required string Name { get; init; }
    public string? Id { get; init; }
    public string? Text { get; init; }
    public IReadOnlyDictionary<string, string>? Fields { get; init; }
}

public sealed class NewUiSlotItem
{
    public string? Id { get; init; }
    public string? Action { get; init; }
    public Dictionary<string, string> Text { get; init; } = new();
    public HashSet<string> Parts { get; init; } = new();
    public Dictionary<string, List<NewUiSlotItem>> Items { get; init; } = new();
    public string? State { get; init; }
}

public sealed class NewUiScene : UserControl
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly SceneDocument Document = JsonSerializer.Deserialize<SceneDocument>(SceneJson, Json)
        ?? throw new InvalidOperationException("The New UI scene document is empty.");

    private readonly Dictionary<string, string> _text = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<NewUiSlotItem>> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _content = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _state = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextBox> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _bound = new(StringComparer.Ordinal);
    private string _scene = "";
    private bool _applying;

    public event EventHandler<NewUiActionEventArgs>? ActionInvoked;

    public string SceneId => _scene;

    public void Apply(
        string scene,
        IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, IReadOnlyList<NewUiSlotItem>> items,
        IReadOnlyDictionary<string, string> state)
    {
        _scene = scene;
        _text.Clear();
        foreach (var pair in text) _text[pair.Key] = pair.Value;
        _items.Clear();
        foreach (var pair in items) _items[pair.Key] = pair.Value;
        _state.Clear();
        foreach (var pair in state) _state[pair.Key] = pair.Value;
        Rebuild();
    }

    public void SetContent(string slot, Control control)
    {
        _content[slot] = control;
        Rebuild();
    }

    public Control? DetachContent(string slot)
    {
        if (!_content.Remove(slot, out var control)) return null;
        Detach(control);
        return control;
    }

    private void Rebuild()
    {
        if (_applying) return;
        _applying = true;
        foreach (var pair in _fields)
            _drafts[pair.Key] = pair.Value.Text ?? "";
        _fields.Clear();
        try
        {
            Content = Document.Scenes.TryGetValue(_scene, out var tree)
                ? Fill(RenderList(tree, null))
                : null;
        }
        finally
        {
            _applying = false;
        }
    }

    private static Control Fill(IReadOnlyList<Control> children)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var child = children.Count == 1 ? children[0] : Host(children);
        child.HorizontalAlignment = HorizontalAlignment.Stretch;
        child.VerticalAlignment = VerticalAlignment.Stretch;
        grid.Children.Add(child);
        return grid;
    }

    private static Control Host(IReadOnlyList<Control> children)
    {
        var panel = new StackPanel();
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    private List<Control> RenderList(IReadOnlyList<SceneNode> nodes, NewUiSlotItem? item)
    {
        var controls = new List<Control>();
        foreach (var node in nodes)
        {
            var control = Render(node, item);
            if (control is not null)
            {
                if (node.Push == true) control.SetValue(ScenePushProperty, true);
                if (node.Grow == true) control.SetValue(SceneGrowProperty, true);
                controls.Add(control);
            }
        }
        return controls;
    }

    private Control? Render(SceneNode node, NewUiSlotItem? item)
    {
        if (node.T == "svg") return Icon(node.D);
        if (node.T is "style" or "script" or "path" or "circle" or "#text") return node.T == "#text" ? new TextBlock { Text = node.X ?? "" } : null;
        if (!string.IsNullOrEmpty(node.When) && !_state.ContainsKey(node.When)) return null;
        if (!string.IsNullOrEmpty(node.Empty) && _items.TryGetValue(node.Empty, out var filled) && filled.Count > 0) return null;
        if (node.For is { Count: > 0 } && !(item?.Action is { } forAction && node.For.Contains(forAction))) return null;
        if (!string.IsNullOrEmpty(node.Part) && item is not null && !item.Parts.Contains(node.Part)) return null;
        var slot = node.Slot ?? node.Demo;
        if (!string.IsNullOrEmpty(node.Repeat))
        {
            var items = item is not null && slot is not null && item.Items.TryGetValue(slot, out var nested)
                ? nested
                : slot is not null && _items.TryGetValue(slot, out var top) ? top : Array.Empty<NewUiSlotItem>();
            items = Grouped(node.Groups, items);
            var rows = new List<Control>();
            if (Document.Templates.TryGetValue(node.Repeat, out var template))
            {
                foreach (var child in items)
                {
                    var row = Host(RenderList(template.K ?? [], child));
                    if (!string.IsNullOrEmpty(node.Action) && string.IsNullOrEmpty(child.Action))
                    {
                        var id = child.Id;
                        var name = node.Action;
                        var press = new Button
                        {
                            Classes = { "au-scene" },
                            Content = row,
                            Background = Brushes.Transparent,
                            Padding = new Thickness(0),
                            ClickMode = ClickMode.Press,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        };
                        press.Click += (_, args) =>
                        {
                            if (!ReferenceEquals(args.Source, press)) return;
                            ActionInvoked?.Invoke(this, new NewUiActionEventArgs { Name = name, Id = id });
                        };
                        rows.Add(press);
                    }
                    else rows.Add(row);
                }
            }
            var repeated = Layout(node.Layout, rows, node.C ?? [], node.Flow);
            return repeated;
        }
        if (node.Content == true && node.Slot is not null && _content.TryGetValue(node.Slot, out var hosted))
        {
            Detach(hosted);
            hosted.HorizontalAlignment = HorizontalAlignment.Stretch;
            hosted.VerticalAlignment = VerticalAlignment.Stretch;
            return Layout("fill", new List<Control> { hosted }, new List<string>(node.C ?? []), node.Flow);
        }
        var labelled = node.Labels is not null && item?.Action is { } labelAction && node.Labels.TryGetValue(labelAction, out var actionLabel) ? actionLabel : null;
        var text = node.Text is not null
            ? item is not null && item.Text.TryGetValue(node.Text, out var fromItem) ? fromItem
            : labelled ?? (_text.TryGetValue(node.Text, out var fromModel) ? fromModel : node.X ?? "")
            : labelled ?? node.X;
        var classes = new List<string>(node.C ?? []);
        if (node.Tone is not null && item?.Action is { } toneAction && node.Tone.TryGetValue(toneAction, out var tone)) classes.Add(tone);
        // A selection names a fixed index, or @id: the row whose id the state names is selected.
        if (node.Selection is { Count: 3 } selection
            && _state.TryGetValue(selection[0], out var selected)
            && selected == (selection[1] == "@id" ? item?.Id : selection[1]))
            classes.Add(selection[2]);
        foreach (var flag in node.Flags ?? [])
        {
            if (flag.Count == 2 && _state.ContainsKey(flag[0])) classes.Add(flag[1]);
        }
        // The catalog bakes its default selection into the markup; once the state names another
        // member of the group, that baked class has to go.
        if (node.Selection is { Count: 3 } baked
            && baked[1] != "@id"
            && _state.TryGetValue(baked[0], out var chosen)
            && chosen != baked[1])
            classes.Remove(baked[2]);
        // A block that holds nothing but words is one line of copy, painted by its own class.
        var words = node.K is { Count: > 0 } kids && kids.All(kid => kid.T == "#text")
            ? string.Join(" ", kids.Select(kid => kid.X)).Trim()
            : "";
        if (string.IsNullOrEmpty(text) && words.Length > 0) text = words;
        var children = words.Length > 0 ? new List<Control>() : RenderList(node.K ?? [], item);
        if (classes.Contains("au-screen-toolbar") && children.Count == 0) return null;
        // A row's state names its host; a status dot inside it takes only a dot state.
        if (!string.IsNullOrEmpty(item?.State) && (node.KindHost == true || (node.C?.Contains("au-workspace") ?? false)
            || ((node.C?.Contains("au-status-dot") ?? false) && item.State.StartsWith("au-status-dot", StringComparison.Ordinal))))
            classes.Add(item.State);
        if (node.Selection is { Count: 3 } appSelection
            && appSelection[0] == "app"
            && appSelection[1] != "@id"
            && !(_state.TryGetValue("app", out var appName) && appName == appSelection[1]))
            classes.Remove(appSelection[2]);
        var action = string.IsNullOrEmpty(node.Action) ? null : (item?.Action ?? node.Action);
        if (node.T == "input" && !string.IsNullOrEmpty(node.Action))
            return Field(node);
        if (node.T == "input")
            return AddressField(text, classes);
        if (!string.IsNullOrEmpty(action) && !InlineTags.Contains(node.T ?? "") && (node.Flow is null || node.Flow.D == "column"))
        {
            // A card that opens something is still a card: it stacks, spans its column, and keeps
            // the face its classes paint.
            var body = new List<Control>();
            if (!string.IsNullOrEmpty(text))
            {
                var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
                body.Add(label);
            }
            body.AddRange(children);
            var face = Layout(string.IsNullOrEmpty(node.Layout) || node.Layout is "column" or "fill" ? "" : node.Layout, body, classes, node.Flow);
            var card = new Button
            {
                Classes = { "au-scene" },
                Content = face,
                ClickMode = ClickMode.Press,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
            };
            if (!string.IsNullOrEmpty(node.Label)) AutomationProperties.SetName(card, node.Label);
            var cardId = item?.Id ?? node.Id;
            // Click bubbles: a button inside the card (Play on a flow) raises it here too. The card
            // answers only its own click, or one press would run both actions.
            card.Click += (_, args) =>
            {
                if (!ReferenceEquals(args.Source, card)) return;
                ActionInvoked?.Invoke(this, new NewUiActionEventArgs { Name = action, Id = cardId });
            };
            return card;
        }
        if (node.T == "button" || !string.IsNullOrEmpty(action))
        {
            var button = new Button
            {
                Classes = { "au-scene" },
                ClickMode = ClickMode.Press,
                Padding = new Thickness(0),
            };
            var inline = new List<Control>(children);
            if (!string.IsNullOrEmpty(text))
                inline.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            var content = ArrangeButton(node.Flow, inline, classes);
            if (HostsFace(classes))
            {
                var face = new Border
                {
                    Child = content,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Paint(face, classes);
                button.Content = face;
                button.Background = Brushes.Transparent;
                button.BorderThickness = new Thickness(0);
                button.MinWidth = 0;
                button.MinHeight = 0;
            }
            else
            {
                button.Content = content;
                Paint(button, classes);
                if (classes.Contains("aun-nav"))
                {
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.HorizontalContentAlignment = HorizontalAlignment.Left;
                }
            }
            var id = item?.Id ?? node.Id;
            var name = action ?? "";
            if (item?.Parts.Contains("locked") == true)
            {
                button.IsEnabled = false;
                button.Opacity = 0.4;
            }
            if (name == "toggle-validation")
            {
                button.MinWidth = 32;
                button.MinHeight = 32;
            }
            button.Click += (_, args) =>
            {
                if (!ReferenceEquals(args.Source, button)) return;
                ActionInvoked?.Invoke(this, new NewUiActionEventArgs
                {
                    Name = name,
                    Id = id,
                    Text = _fields.TryGetValue(name, out var field) ? field.Text : null,
                    Fields = FieldValues(),
                });
            };
            return button;
        }
        if (classes.Contains("au-chrome-mark"))
            return Mark();
        if (classes.Contains("aun-state-map__drop"))
            return Drop();
        if (classes.Contains("aun-task__grip"))
            return Grip();
        if (classes.Contains("aun-state-map__crossbar"))
            return Crossbar(_items.TryGetValue("fleet", out var lanes) ? lanes.Count : 4);
        if (classes.Contains("aun-state-map__root-title") && !string.IsNullOrEmpty(text))
            return FleetTitle(text, classes);
        if (!string.IsNullOrEmpty(text) && children.Count == 0)
        {
            var block = node.Nowrap == true
                ? new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis }
                : new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            Paint(block, classes);
            return block;
        }
        var kind = node.Layout;
        if (string.IsNullOrEmpty(kind) && node.Flow?.D == "row") kind = "row";
        if (string.IsNullOrEmpty(kind) && node.Flow?.J == "between") kind = "spread";
        var layout = Layout(kind, children, classes, node.Flow);
        return layout;
    }

    private static Control Layout(string? kind, IReadOnlyList<Control> children, IReadOnlyList<string> classes, SceneFlow? flow)
    {
        Control laid;
        if (kind == "chrome")
        {
            var bar = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,*"),
                MinHeight = 42,
            };
            for (var i = 0; i < children.Count && i < 3; i++)
            {
                Grid.SetColumn(children[i], i);
                children[i].VerticalAlignment = VerticalAlignment.Center;
                if (i == 1) children[i].HorizontalAlignment = HorizontalAlignment.Center;
                if (i == 2) children[i].HorizontalAlignment = HorizontalAlignment.Right;
                bar.Children.Add(children[i]);
            }
            laid = bar;
        }
        else if (kind == "lanes")
        {
            var lanes = new Grid();
            for (var i = 0; i < children.Count; i++)
            {
                lanes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(children[i], i);
                children[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                var last = i == children.Count - 1;
                children[i].Margin = new Thickness(i == 0 ? 0 : 6, 0, last ? 0 : 6, 0);
                lanes.Children.Add(children[i]);
            }
            laid = lanes;
        }
        else if (kind == "spread" || kind == "trio")
        {
            var spread = kind == "spread";
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(spread ? "*,Auto" : "Auto,*,Auto"),
            };
            for (var i = 0; i < children.Count; i++)
            {
                var column = spread
                    ? (i == children.Count - 1 && children.Count > 1 ? 1 : 0)
                    : (children.Count >= 3 ? Math.Min(i, 2) : i == children.Count - 1 ? 2 : i);
                Grid.SetColumn(children[i], column);
                children[i].VerticalAlignment = VerticalAlignment.Center;
                if (spread && column == 0) children[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                if (!spread && column == 1)
                {
                    children[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                    children[i].MinWidth = 0;
                }
                if (i > 0) children[i].Margin = new Thickness(spread ? 12 : 8, 0, 0, 0);
                grid.Children.Add(children[i]);
            }
            laid = grid;
        }
        else if (kind == "pair")
        {
            var pair = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            for (var i = 0; i < children.Count && i < 2; i++)
            {
                Grid.SetColumn(children[i], i);
                children[i].VerticalAlignment = VerticalAlignment.Center;
                if (i == 1)
                {
                    children[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                    children[i].MinWidth = 0;
                }
                if (i > 0) children[i].Margin = new Thickness(8, 0, 0, 0);
                pair.Children.Add(children[i]);
            }
            laid = pair;
        }
        else if (kind == "grid")
        {
            var aside = children.Count > 2;
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(aside ? "264,*,Auto" : "264,*"),
            };
            for (var i = 0; i < children.Count; i++)
            {
                Grid.SetColumn(children[i], i);
                grid.Children.Add(children[i]);
            }
            laid = grid;
        }
        else if (kind == "row")
        {
            var gap = flow?.G ?? 8;
            var pushed = children.Where(child => child.GetValue(ScenePushProperty)).ToList();
            if (pushed.Count > 0)
            {
                var dock = new DockPanel { LastChildFill = true };
                var trailing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
                foreach (var child in pushed)
                {
                    Align(child, flow, horizontal: true);
                    trailing.Children.Add(child);
                }
                DockPanel.SetDock(trailing, Dock.Right);
                trailing.VerticalAlignment = VerticalAlignment.Center;
                dock.Children.Add(trailing);
                // The leading controls take what the trailing ones leave, and the last of them fills
                // it, so a long name trims at the trailing controls instead of running under them.
                var leading = new DockPanel { LastChildFill = true, VerticalAlignment = VerticalAlignment.Center };
                var front = children.Where(child => !child.GetValue(ScenePushProperty)).ToList();
                for (var i = 0; i < front.Count; i++)
                {
                    Align(front[i], flow, horizontal: true);
                    DockPanel.SetDock(front[i], Dock.Left);
                    if (i < front.Count - 1) front[i].Margin = new Thickness(front[i].Margin.Left, front[i].Margin.Top, front[i].Margin.Right + gap, front[i].Margin.Bottom);
                    else front[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                    leading.Children.Add(front[i]);
                }
                dock.Children.Add(leading);
                laid = dock;
            }
            else if (children.Any(child => child.GetValue(SceneGrowProperty)))
            {
                var grid = new Grid();
                for (var i = 0; i < children.Count; i++)
                {
                    var grow = children[i].GetValue(SceneGrowProperty);
                    grid.ColumnDefinitions.Add(new ColumnDefinition
                    {
                        Width = grow ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
                    });
                    Grid.SetColumn(children[i], i);
                    Align(children[i], flow, horizontal: true);
                    if (grow) children[i].HorizontalAlignment = HorizontalAlignment.Stretch;
                    if (i > 0)
                    {
                        var margin = children[i].Margin;
                        children[i].Margin = new Thickness(margin.Left + gap, margin.Top, margin.Right, margin.Bottom);
                    }
                    grid.Children.Add(children[i]);
                }
                laid = grid;
            }
            else if (flow?.W == "wrap")
            {
                // flex-wrap: wrap. A long label moves to the next line instead of off the edge.
                var wrap = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = gap, LineSpacing = gap };
                foreach (var child in children)
                {
                    Align(child, flow, horizontal: true);
                    wrap.Children.Add(child);
                }
                laid = wrap;
            }
            else
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
                foreach (var child in children)
                {
                    Align(child, flow, horizontal: true);
                    row.Children.Add(child);
                }
                laid = row;
            }
        }
        else if (kind is "fill" or "column")
        {
            var rows = new Grid();
            var fillAt = -1;
            for (var i = 0; i < children.Count; i++)
            {
                rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                if (Fills(children[i])) fillAt = i;
            }
            if (kind == "fill" && fillAt < 0 && children.Count == 1)
                fillAt = 0;
            if (kind == "fill" && fillAt >= 0)
                rows.RowDefinitions[fillAt].Height = new GridLength(1, GridUnitType.Star);
            if (flow?.G is double columnGap && columnGap > 0)
            {
                for (var i = 1; i < children.Count; i++)
                {
                    var margin = children[i].Margin;
                    children[i].Margin = new Thickness(margin.Left, margin.Top + columnGap, margin.Right, margin.Bottom);
                }
            }
            for (var i = 0; i < children.Count; i++)
            {
                Grid.SetRow(children[i], i);
                if (classes.Contains("au-validation-sidebar--collapsed"))
                    children[i].HorizontalAlignment = HorizontalAlignment.Center;
                rows.Children.Add(children[i]);
            }
            if (classes.Contains("aun-settings__list"))
            {
                for (var i = 1; i < rows.Children.Count; i++)
                {
                    if (rows.Children[i] is Border rule)
                    {
                        rule.BorderThickness = new Thickness(0, 1, 0, 0);
                        rule.BorderBrush = ThemeBrush("AgentUpColorBorderSubtleBrush");
                    }
                }
            }
            ScrollViewer? columnScroll = null;
            if (kind == "column" && !classes.Contains("aun-settings__list"))
            {
                columnScroll = new ScrollViewer { Content = rows };
                if (classes.Contains("au-validation-sidebar"))
                    columnScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
            laid = columnScroll ?? (Control)rows;
        }
        else
        {
            var stack = new StackPanel { Spacing = classes.Contains("aun-settings__subs") ? 0 : 4 };
            foreach (var child in children) stack.Children.Add(child);
            if (classes.Contains("aun-settings__subs"))
            {
                for (var i = 0; i < stack.Children.Count; i++)
                {
                    if (stack.Children[i] is Border rule)
                    {
                        rule.BorderThickness = new Thickness(0, 1, 0, 0);
                        rule.BorderBrush = ThemeBrush("AgentUpColorBorderSubtleBrush");
                        rule.Padding = new Thickness(16, 0, 0, 0);
                    }
                }
            }
            laid = stack;
        }
        var framed = Frame(laid, classes);
        Place(framed, classes);
        return framed;
    }

    private static void Place(Control control, IReadOnlyList<string> classes)
    {
        if (classes.Contains("aun-state-map__stem") || classes.Contains("aun-state-map__root"))
            control.HorizontalAlignment = HorizontalAlignment.Center;
        if (classes.Contains("aun-state-map__root")) control.MaxWidth = 256;
        if (classes.Contains("au-title-tool") && control is Border tool && tool.Child is Control icon)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            icon.VerticalAlignment = VerticalAlignment.Center;
        }
        if (classes.Contains("aun-settings") || classes.Contains("aun-task-board"))
        {
            // width: min(100%, 36rem), centred: a stretched box capped by MaxWidth sits centred.
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.MaxWidth = 576;
        }
        if (classes.Contains("au-validation-sidebar"))
            control.ClipToBounds = true;
        if (classes.Contains("au-validation-flow") || classes.Contains("au-validation-flow-head") || classes.Contains("au-validation-copy"))
        {
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.MinWidth = 0;
        }
    }

    /// <summary>
    /// Rows in the order the scene declares their groups, the first row of each group carrying
    /// the group's label and count as its section. Rows whose action names no group follow.
    /// </summary>
    private static IReadOnlyList<NewUiSlotItem> Grouped(List<List<string>>? groups, IReadOnlyList<NewUiSlotItem> items)
    {
        if (groups is not { Count: > 0 }) return items;
        var output = new List<NewUiSlotItem>();
        foreach (var group in groups)
        {
            var rows = items.Where(row => row.Action == group[0]).ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                if (i > 0)
                {
                    output.Add(rows[i]);
                    continue;
                }
                output.Add(new NewUiSlotItem
                {
                    Id = rows[i].Id,
                    Action = rows[i].Action,
                    State = rows[i].State,
                    Items = rows[i].Items,
                    Parts = new HashSet<string>(rows[i].Parts, StringComparer.Ordinal) { "section" },
                    Text = new Dictionary<string, string>(rows[i].Text, StringComparer.Ordinal) { ["section"] = $"{group[1]} · {rows.Count}" },
                });
            }
        }
        var named = groups.Select(group => group[0]).ToHashSet(StringComparer.Ordinal);
        output.AddRange(items.Where(row => row.Action is null || !named.Contains(row.Action)));
        return output;
    }

    private static readonly HashSet<string> InlineTags = new(StringComparer.Ordinal) { "button", "a", "span", "label", "strong", "em", "code" };

    private static bool HostsFace(IReadOnlyList<string> classes) =>
        classes.Any(name => IconFace(name) || name is "au-app-tab" or "au-subtab");

    private static bool IconFace(string name) =>
        name is "au-lifecycle-button" or "au-chrome-button" or "au-chrome-close" or "au-workspace-add" or "au-workspace-delete" or "au-title-tool" or "au-browser-button";

    private static Bitmap? _logo;

    private static Control Mark()
    {
        _logo ??= new Bitmap(AssetLoader.Open(new Uri("avares://AgentUp.Desktop/media/logo.png")));
        return new Image
        {
            Source = _logo,
            Width = 18,
            Height = 18,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static Control Drop()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new Border
        {
            Width = 2,
            Height = 16,
            Background = ThemeBrush("AgentUpColorTextFaintBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        stack.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = ThemeBrush("AgentUpColorTextFaintBrush"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, -4, 0, 0),
        });
        return stack;
    }

    /// <summary>
    /// The bar joins the first lane's drop to the last: inset by half a lane at each end, so a
    /// single lane has nothing to join and the bar has no length.
    /// </summary>
    private static Control Crossbar(int lanes)
    {
        var inset = new Grid { Height = 2 };
        inset.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inset.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0, 2 * lanes - 2), GridUnitType.Star) });
        inset.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var bar = new Border
        {
            Height = 2,
            Background = ThemeBrush("AgentUpColorTextFaintBrush"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(bar, 1);
        inset.Children.Add(bar);
        return inset;
    }

    private static Control Grip()
    {
        var grip = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 3; i++)
            grip.Children.Add(new Border { Width = 18, Height = 2, CornerRadius = new CornerRadius(1), Background = ThemeBrush("AgentUpColorTextMutedBrush") });
        return grip;
    }

    private static Control FleetTitle(string text, IReadOnlyList<string> classes)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = ThemeBrush("AgentUpColorStatusHealthyBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var block = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        Paint(block, classes);
        row.Children.Add(block);
        return row;
    }

    private static Control? Icon(IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0) return null;
        var canvas = new Canvas { Width = 24, Height = 24 };
        foreach (var data in paths)
        {
            var geometry = IconGeometry(data);
            if (geometry is not null)
            {
                canvas.Children.Add(new Avalonia.Controls.Shapes.Path
                {
                    Data = geometry,
                    Stroke = ThemeBrush("AgentUpColorTextPrimaryBrush"),
                    StrokeThickness = 1.75,
                    StrokeJoin = PenLineJoin.Round,
                    StrokeLineCap = PenLineCap.Round,
                });
            }
        }
        if (canvas.Children.Count == 0) return null;
        return new Viewbox { Width = 16, Height = 16, Stretch = Stretch.Uniform, Child = canvas };
    }

    private static Geometry? IconGeometry(string data)
    {
        try
        {
            return Geometry.Parse(data);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private Control Field(SceneNode node)
    {
        var action = node.Action!;
        var boundValue = !string.IsNullOrEmpty(node.Text) && _text.TryGetValue(node.Text, out var fromModel)
            ? fromModel
            : "";
        if (!string.IsNullOrEmpty(node.Text))
        {
            if (!_bound.TryGetValue(action, out var previous) || previous != boundValue)
            {
                _drafts[action] = boundValue;
                _bound[action] = boundValue;
            }
        }
        else if (!_drafts.ContainsKey(action) && boundValue.Length > 0)
            _drafts[action] = boundValue;
        var box = new TextBox
        {
            MinWidth = 0,
            PlaceholderText = node.Placeholder ?? "",
            PasswordChar = node.Secret == true ? '•' : default,
            Text = _drafts.TryGetValue(action, out var draft) ? draft : boundValue,
        };
        box.TextChanged += (_, _) => _drafts[action] = box.Text ?? "";
        Paint(box, node.C ?? []);
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.VerticalAlignment = VerticalAlignment.Center;
        _fields[action] = box;
        return box;
    }

    private Dictionary<string, string> FieldValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in _fields)
            values[pair.Key] = pair.Value.Text ?? "";
        return values;
    }

    private Control AddressField(string? text, IReadOnlyList<string> classes)
    {
        var box = new TextBox { Text = text ?? "", MinWidth = 0 };
        Paint(box, classes);
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.VerticalAlignment = VerticalAlignment.Center;
        box.SetValue(SceneGrowProperty, true);
        box.KeyDown += (_, key) =>
        {
            if (key.Key != Key.Enter) return;
            ActionInvoked?.Invoke(this, new NewUiActionEventArgs { Name = "navigate-address", Text = box.Text });
            key.Handled = true;
        };
        return box;
    }

    private static IBrush ThemeBrush(string key) =>
        Application.Current?.TryGetResource(key, null, out var value) == true && value is IBrush brush
            ? brush
            : Brushes.Transparent;

    private static bool Fills(Control control) =>
        control is Border border && (
            border.Classes.Contains("au-screen-body")
            || border.Classes.Contains("au-screen-pane")
            || border.Classes.Contains("aun-phone__body")
            || border.Classes.Contains("aun-rail__nav")
            || border.Classes.Contains("aun-application"));

    private static readonly AttachedProperty<bool> ScenePushProperty =
        AvaloniaProperty.RegisterAttached<NewUiScene, Control, bool>("ScenePush");

    private static readonly AttachedProperty<bool> SceneGrowProperty =
        AvaloniaProperty.RegisterAttached<NewUiScene, Control, bool>("SceneGrow");

    private static void Align(Control child, SceneFlow? flow, bool horizontal)
    {
        if (flow?.A == "center" && horizontal) child.VerticalAlignment = VerticalAlignment.Center;
        if (flow?.A == "start" && horizontal) child.VerticalAlignment = VerticalAlignment.Top;
        if (flow?.A == "center" && !horizontal) child.HorizontalAlignment = HorizontalAlignment.Center;
    }

    /// <summary>
    /// Button content is inline unless its own CSS says otherwise. A status mark and its label
    /// therefore sit on one line, the way the catalog writes them.
    /// </summary>
    private static Control ArrangeButton(SceneFlow? flow, IReadOnlyList<Control> children, IReadOnlyList<string> classes)
    {
        foreach (var child in children) DressLabel(child, classes);
        if (children.Count == 0) return new Panel();
        if (children.Count == 1) return children[0];
        var column = flow?.D == "column";
        var stack = new StackPanel
        {
            Orientation = column ? Orientation.Vertical : Orientation.Horizontal,
            Spacing = flow?.G ?? (column ? 2 : 6),
        };
        foreach (var child in children)
        {
            if (column) child.HorizontalAlignment = HorizontalAlignment.Center;
            else child.VerticalAlignment = VerticalAlignment.Center;
            stack.Children.Add(child);
        }
        return stack;
    }

    /// <summary>
    /// The catalog puts color and type on the control. The label is a separate text block, so it
    /// carries the same classes, minus the box padding the face already applied.
    /// </summary>
    private static void DressLabel(Control child, IReadOnlyList<string> classes)
    {
        if (child is not TextBlock block || block.Classes.Count > 0) return;
        if (block.Text is { } label) block.Text = label.Trim();
        if (classes.Any(IconFace))
        {
            block.Foreground = ThemeBrush("AgentUpColorTextPrimaryBrush");
            block.HorizontalAlignment = HorizontalAlignment.Center;
            block.VerticalAlignment = VerticalAlignment.Center;
            block.TextAlignment = TextAlignment.Center;
            return;
        }
        Paint(block, classes);
        block.Padding = new Thickness(0);
        block.MinHeight = 0;
        block.VerticalAlignment = VerticalAlignment.Center;
        block.Background = Brushes.Transparent;
    }

    private static Control Frame(Control child, IReadOnlyList<string> classes)
    {
        var border = new Border { Child = child };
        Paint(border, classes);
        return border;
    }

    private static void Paint(Control control, IReadOnlyList<string> classes)
    {
        foreach (var name in classes) control.Classes.Add(name);
    }

    private static void Detach(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case ContentControl content when content.Content == control:
                content.Content = null;
                break;
            case Decorator decorator when decorator.Child == control:
                decorator.Child = null;
                break;
        }
    }

    private const string SceneJson = SCENE_JSON_PLACEHOLDER;
}

internal sealed class SceneDocument
{
    public Dictionary<string, List<SceneNode>> Scenes { get; set; } = new();
    public Dictionary<string, SceneNode> Templates { get; set; } = new();
}

internal sealed class SceneNode
{
    public string? T { get; set; }
    public List<string>? C { get; set; }
    public string? X { get; set; }
    public string? Slot { get; set; }
    public string? Text { get; set; }
    public string? Part { get; set; }
    public string? Action { get; set; }
    public string? Label { get; set; }
    public string? Id { get; set; }
    public List<string>? Actions { get; set; }
    public string? Repeat { get; set; }
    public string? Demo { get; set; }
    public string? When { get; set; }
    public bool? Content { get; set; }
    public string? Layout { get; set; }
    public List<string>? Selection { get; set; }
    public List<List<string>>? Flags { get; set; }
    public bool? KindHost { get; set; }
    public List<string>? D { get; set; }
    public bool? Push { get; set; }
    public bool? Grow { get; set; }
    public bool? Nowrap { get; set; }
    public List<List<string>>? Groups { get; set; }
    public Dictionary<string, string>? Tone { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
    public List<string>? For { get; set; }
    public string? Empty { get; set; }
    public bool? Secret { get; set; }
    public string? Placeholder { get; set; }
    public SceneFlow? Flow { get; set; }
    public List<SceneNode>? K { get; set; }
}

internal sealed class SceneFlow
{
    public string? D { get; set; }
    public string? A { get; set; }
    public string? J { get; set; }
    public double? G { get; set; }
    public string? W { get; set; }
}
`;
  return source.replace('SCENE_JSON_PLACEHOLDER', JSON.stringify(payload));
}
