/**
 * The New UI scene is the page.
 *
 * `assembleScreens` already turns a scene into one component tree for the web showcase.
 * This walks that same assembly — layout slots, then `data-au-use` — and emits the holes
 * (slot, state, action) plus the Avalonia and React Native scene that mount the tree.
 * Clients bind Server state into the holes. They do not draw the page again.
 */

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

const columnFill = new Set(['au-screen', 'aun-phone', 'au-screen-main']);
const gridBody = new Set(['au-screen-body']);
const chromeLayout = new Set(['au-chrome']);
const rowLayout = new Set([
  'au-screen-tabs', 'au-screen-subtabs', 'au-screen-toolbar', 'au-mobile-tab-bar', 'au-tabs',
  'aun-nav-row', 'aun-agent-head', 'aun-phone__head-row', 'au-validation-dock',
  'au-cluster', 'au-logo-lockup', 'au-workspace', 'au-workspace-head', 'au-badge',
  'au-validation-flow-head', 'aun-settings__row', 'aun-task-row',
  'aun-state-map__head', 'aun-state-map__root', 'aun-state-map__branch-head',
]);
const columnLayout = new Set([
  'au-screen-pane', 'au-screen-rail', 'au-screen-aside', 'aun-phone__body', 'aun-phone__head',
  'aun-settings', 'aun-task-board', 'aun-state-map', 'aun-validate', 'au-validation-sidebar',
  'aun-state-map__lane', 'aun-state-map__branch', 'aun-state-map__branch-copy', 'aun-task-row__body',
]);

/**
 * One scene per entry: the tree both clients mount, and the holes they are allowed to fill.
 * `components` is a Map of catalog id to the component record. New UI records are the
 * pre-demo markup so a demo fill stays a showcase and the holes stay open.
 */
export function sceneBundle(screensHtml, components) {
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
  const contracts = built.map(scene => sceneContract(scene, templates));
  const regions = sceneRegionClasses.filter(name => built.some(scene => hasClass(scene.tree, name)));
  return { scenes: built, templates, contracts, regions };
}

export function sceneContract(scene, templates = {}) {
  const slots = new Map();
  const states = new Set();
  const actions = new Set();
  const visit = (node, insideRepeat) => {
    recordNode(node, slots, states, actions, insideRepeat);
    const nested = insideRepeat || Boolean(node.repeat) || Boolean(node.content);
    for (const child of node.k ?? []) visit(child, nested);
    if (node.repeat && templates[node.repeat]) visit(templates[node.repeat], true);
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
  const actions = data(tag.attrs, 'actions');
  if (actions) node.actions = actions.split(/\s+/).filter(Boolean);
  const repeat = data(tag.attrs, 'repeat');
  if (repeat) node.repeat = repeat;
  const demo = data(tag.attrs, 'demo');
  if (demo) node.demo = demo;
  const when = data(tag.attrs, 'when');
  if (when) node.when = when;
  const selection = data(tag.attrs, 'selection');
  if (selection) {
    const [group, index, modifier] = selection.split(':');
    node.selection = [group, index, modifier];
  }
  const flags = data(tag.attrs, 'flags');
  if (flags) node.flags = flags.split(/\s+/).filter(Boolean).map(entry => entry.split(':'));
  if (data(tag.attrs, 'kind') === '1') node.kindHost = true;
  const layout = layoutOf(node.c ?? []);
  if (layout) node.layout = layout;
  return node;
}

function layoutOf(classes) {
  if (classes.some(name => chromeLayout.has(name))) return 'chrome';
  if (classes.some(name => gridBody.has(name))) return 'grid';
  if (classes.some(name => columnFill.has(name))) return 'fill';
  if (classes.some(name => rowLayout.has(name))) return 'row';
  if (classes.some(name => columnLayout.has(name))) return 'column';
  return '';
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

function paint(classes, kind) {
  const names = (classes ?? []).map(bindingName).filter(name => newUiTheme.components[name]);
  if (!names.length) return undefined;
  return kind === 'text' ? newUiText(...names) : newUiBox(...names);
}

function selected(node, model) {
  if (!node.selection) return false;
  return String(model.state[node.selection[0]] ?? '') === String(node.selection[1]);
}

function flagged(node, model) {
  const extra = [];
  for (const [name, modifier] of node.flags ?? []) {
    if (model.state[name]) extra.push(modifier);
  }
  if (selected(node, model)) extra.push(node.selection[2]);
  return extra;
}

export function createNewUiScene(React, native) {
  const { View, Text, Pressable, ScrollView } = native;
  const h = React.createElement;
  function render(node, model, item, key) {
    if (!node || node.t === 'svg' || node.t === 'style' || node.t === 'script' || node.t === 'path' || node.t === 'circle') return null;
    if (node.t === '#text') return h(Text, { key }, node.x);
    if (node.when && !model.state[node.when]) return null;
    if (node.part && item?.parts && !item.parts.includes(node.part)) return null;
    const slotName = node.slot || node.demo;
    if (node.repeat) {
      const items = (item?.items && item.items[slotName]) || model.items[slotName] || [];
      const template = newUiSceneDocument.templates[node.repeat];
      const rows = items.map((child, index) => {
        const row = render(template, model, child, String(child.id ?? index));
        if (!node.action || child.action) return row;
        return h(Pressable, {
          key: String(child.id ?? index),
          accessibilityRole: 'button',
          onPress: () => model.onAction(node.action, child.id ?? null),
          style: { alignSelf: 'stretch' },
        }, row);
      });
      return h(View, { key, style: [paint(node.c), layoutStyle(node.layout)] }, rows);
    }
    if (node.content && model.content && model.content[node.slot] != null) return model.content[node.slot];
    const classes = [...(node.c ?? []), ...flagged(node, model)];
    if (item?.state && (node.kindHost || (node.c ?? []).includes('au-workspace'))) classes.push(item.state);
    const textValue = node.text
      ? (item?.text?.[node.text] ?? model.text[node.text] ?? node.x ?? '')
      : node.x;
    const children = (node.k ?? []).map((child, index) => render(child, model, item, index)).filter(child => child != null);
    const action = node.action ? (item?.action || node.action) : null;
    if (node.t === 'button' || action) {
      return h(Pressable, {
        key,
        accessibilityRole: 'button',
        onPress: () => model.onAction(action, item?.id ?? null),
        style: paint(classes),
      }, textValue ? h(Text, { style: paint(classes, 'text') }, textValue) : children);
    }
    if (textValue && !(node.k ?? []).some(child => child.t !== '#text')) {
      return h(Text, { key, style: paint(classes, 'text') }, textValue);
    }
    const body = textValue ? [h(Text, { key: 'label', style: paint(classes, 'text') }, textValue), ...children] : children;
    const grows = (node.c ?? []).some(name => name === 'au-screen-body' || name === 'au-screen-pane' || name === 'aun-phone__body' || name === 'aun-application');
    const arranged = arrange(h, View, body, node.layout);
    if (node.layout === 'column') {
      return h(ScrollView, { key, style: [{ flex: grows ? 1 : undefined }, paint(classes)] }, arranged);
    }
    return h(View, {
      key,
      style: [paint(classes), measure(node.c), layoutStyle(node.layout), grows ? { flex: 1 } : null],
    }, arranged);
  }
  function layoutStyle(layout) {
    if (layout === 'fill' || layout === 'column') return { flex: layout === 'fill' ? 1 : undefined, flexDirection: 'column' };
    if (layout === 'row') return { flexDirection: 'row', alignItems: 'center' };
    if (layout === 'chrome') return { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' };
    if (layout === 'grid' || layout === 'lanes') return { flexDirection: 'row', flex: 1, alignItems: 'stretch' };
    return { flexDirection: 'column' };
  }
  function measure(classes) {
    const names = classes ?? [];
    if (names.includes('aun-settings')) return { alignSelf: 'center', width: '100%', maxWidth: 576 };
    if (names.includes('aun-state-map__root')) return { alignSelf: 'center', maxWidth: 256 };
    if (names.includes('aun-state-map__stem') || names.includes('aun-state-map__drop')) return { alignSelf: 'center' };
    return null;
  }
  function arrange(create, Box, children, layout) {
    if (layout === 'grid') {
      return children.map((child, index) => create(Box, {
        key: 'col-' + index,
        style: index === 0 ? { width: 208 } : index === children.length - 1 && children.length > 2 ? null : { flex: 1 },
      }, child));
    }
    if (layout === 'lanes') {
      return children.map((child, index) => create(Box, { key: 'lane-' + index, style: { flex: 1, minWidth: 0 } }, child));
    }
    return children;
  }
  /**
   * @param {{ scene: string, text?: Record<string, string>, items?: Record<string, unknown[]>, state?: Record<string, string>, content?: Record<string, unknown>, onAction?: (name: string, id: string | null) => void }} props
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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgentUp.Desktop.DesignSystem;

public sealed class NewUiActionEventArgs : EventArgs
{
    public required string Name { get; init; }
    public string? Id { get; init; }
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
            if (control is not null) controls.Add(control);
        }
        return controls;
    }

    private Control? Render(SceneNode node, NewUiSlotItem? item)
    {
        if (node.T is "svg" or "style" or "script" or "path" or "circle" or "#text") return node.T == "#text" ? new TextBlock { Text = node.X ?? "" } : null;
        if (!string.IsNullOrEmpty(node.When) && !_state.ContainsKey(node.When)) return null;
        if (!string.IsNullOrEmpty(node.Part) && item is not null && !item.Parts.Contains(node.Part)) return null;
        var slot = node.Slot ?? node.Demo;
        if (!string.IsNullOrEmpty(node.Repeat))
        {
            var items = item is not null && slot is not null && item.Items.TryGetValue(slot, out var nested)
                ? nested
                : slot is not null && _items.TryGetValue(slot, out var top) ? top : Array.Empty<NewUiSlotItem>();
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
                            Content = row,
                            Background = Brushes.Transparent,
                            Padding = new Thickness(0),
                            ClickMode = ClickMode.Press,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        };
                        press.Click += (_, _) => ActionInvoked?.Invoke(this, new NewUiActionEventArgs { Name = name, Id = id });
                        rows.Add(press);
                    }
                    else rows.Add(row);
                }
            }
            var repeated = Layout(node.Layout, rows, node.C ?? []);
            return repeated;
        }
        if (node.Content == true && node.Slot is not null && _content.TryGetValue(node.Slot, out var hosted))
        {
            Detach(hosted);
            return hosted;
        }
        var text = node.Text is not null
            ? item is not null && item.Text.TryGetValue(node.Text, out var fromItem) ? fromItem
            : _text.TryGetValue(node.Text, out var fromModel) ? fromModel
            : node.X ?? ""
            : node.X;
        var classes = new List<string>(node.C ?? []);
        if (node.Selection is { Count: 3 } selection
            && _state.TryGetValue(selection[0], out var selected)
            && selected == selection[1])
            classes.Add(selection[2]);
        foreach (var flag in node.Flags ?? [])
        {
            if (flag.Count == 2 && _state.ContainsKey(flag[0])) classes.Add(flag[1]);
        }
        var children = RenderList(node.K ?? [], item);
        if (!string.IsNullOrEmpty(item?.State) && (node.KindHost == true || (node.C?.Contains("au-workspace") ?? false)))
            classes.Add(item.State);
        var action = string.IsNullOrEmpty(node.Action) ? null : (item?.Action ?? node.Action);
        if (node.T == "button" || !string.IsNullOrEmpty(action))
        {
            var button = new Button
            {
                Content = string.IsNullOrEmpty(text) ? Host(children) : text,
                ClickMode = ClickMode.Press,
            };
            Paint(button, classes);
            var id = item?.Id;
            var name = action ?? "";
            button.Click += (_, _) => ActionInvoked?.Invoke(this, new NewUiActionEventArgs { Name = name, Id = id });
            return button;
        }
        if (!string.IsNullOrEmpty(text) && children.Count == 0)
        {
            var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
            Paint(block, classes);
            return block;
        }
        var layout = Layout(node.Layout, children, classes);
        return layout;
    }

    private static Control Layout(string? kind, IReadOnlyList<Control> children, IReadOnlyList<string> classes)
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
                lanes.Children.Add(children[i]);
            }
            laid = lanes;
        }
        else if (kind == "grid")
        {
            var aside = children.Count > 2;
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions(aside ? "208,*,Auto" : "208,*"),
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
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var child in children) row.Children.Add(child);
            laid = row;
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
            if (kind == "fill" && fillAt >= 0)
                rows.RowDefinitions[fillAt].Height = new GridLength(1, GridUnitType.Star);
            for (var i = 0; i < children.Count; i++)
            {
                Grid.SetRow(children[i], i);
                rows.Children.Add(children[i]);
            }
            laid = kind == "column" ? new ScrollViewer { Content = rows } : rows;
        }
        else
        {
            var stack = new StackPanel { Spacing = 4 };
            foreach (var child in children) stack.Children.Add(child);
            laid = stack;
        }
        var framed = Frame(laid, classes);
        Place(framed, classes);
        return framed;
    }

    private static void Place(Control control, IReadOnlyList<string> classes)
    {
        if (classes.Contains("aun-state-map__stem") || classes.Contains("aun-state-map__drop") || classes.Contains("aun-state-map__root"))
            control.HorizontalAlignment = HorizontalAlignment.Center;
        if (classes.Contains("aun-state-map__root")) control.MaxWidth = 256;
        if (classes.Contains("aun-settings"))
        {
            control.HorizontalAlignment = HorizontalAlignment.Center;
            control.MaxWidth = 576;
        }
    }

    private static bool Fills(Control control) =>
        control is Border border && (
            border.Classes.Contains("au-screen-body")
            || border.Classes.Contains("au-screen-pane")
            || border.Classes.Contains("aun-phone__body")
            || border.Classes.Contains("aun-application"));

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
    public List<string>? Actions { get; set; }
    public string? Repeat { get; set; }
    public string? Demo { get; set; }
    public string? When { get; set; }
    public bool? Content { get; set; }
    public string? Layout { get; set; }
    public List<string>? Selection { get; set; }
    public List<List<string>>? Flags { get; set; }
    public bool? KindHost { get; set; }
    public List<SceneNode>? K { get; set; }
}
`;
  return source.replace('SCENE_JSON_PLACEHOLDER', JSON.stringify(payload));
}
