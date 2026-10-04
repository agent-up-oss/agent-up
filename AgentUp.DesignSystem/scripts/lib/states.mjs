/**
 * Component state parameterisation.
 *
 * A catalog component declares the selections and flags a screen is allowed to set, and a
 * screen says which member is selected. Before this existed a state was a second component
 * with its own copy of the markup, so four tab strips differing only in which button carried
 * `--selected` cost four copies of the same five buttons and their icons.
 *
 * The declaration lives on the catalog article:
 *
 *   data-au-states="selected=au-app-tab/au-app-tab--selected"   a selection group
 *   data-au-states="collapsed=au-validation-sidebar--collapsed"  a flag on the component root
 *
 * The screen sets it on the region:
 *
 *   data-au-state="selected:2"
 *   data-au-state="selected:1 collapsed"
 *
 * Only the declared modifier classes move. Everything else in the component's markup is
 * reproduced byte for byte, which is what lets the screenshot contract keep checking a scene
 * against the catalog rather than trusting it.
 */

const groupSeparator = '/';

export function parseStateDeclarations(raw) {
  const declarations = new Map();
  for (const entry of (raw ?? '').split(/\s+/).filter(Boolean)) {
    const split = entry.indexOf('=');
    if (split < 1) throw new Error(`State declaration '${entry}' is not '<name>=<spec>'.`);
    const name = entry.slice(0, split);
    const spec = entry.slice(split + 1);
    if (declarations.has(name)) throw new Error(`State '${name}' is declared twice.`);
    if (spec.includes(groupSeparator)) {
      const [member, modifier] = spec.split(groupSeparator);
      if (!member || !modifier) throw new Error(`State '${name}' needs '<member>/<modifier>'.`);
      declarations.set(name, { kind: 'selection', member, modifier });
      continue;
    }
    if (!spec) throw new Error(`State '${name}' has no modifier class.`);
    declarations.set(name, { kind: 'flag', modifier: spec });
  }
  return declarations;
}

export function parseStateRequest(raw) {
  const request = new Map();
  for (const entry of (raw ?? '').split(/\s+/).filter(Boolean)) {
    const split = entry.indexOf(':');
    if (split < 0) {
      if (request.has(entry)) throw new Error(`State '${entry}' is set twice.`);
      request.set(entry, true);
      continue;
    }
    const name = entry.slice(0, split);
    const value = entry.slice(split + 1);
    if (!name) throw new Error(`State request '${entry}' has no name.`);
    if (!/^\d+$/.test(value)) throw new Error(`State '${name}' needs a zero-based index, got '${value}'.`);
    if (request.has(name)) throw new Error(`State '${name}' is set twice.`);
    request.set(name, Number(value));
  }
  return request;
}

/** Every modifier class a component's declaration is allowed to move. */
export function declaredModifiers(declarations) {
  return [...declarations.values()].map(declaration => declaration.modifier);
}

export function applyStates(component, stateRequest) {
  const declarations = parseStateDeclarations(component.states);
  const request = parseStateRequest(stateRequest);
  if (!request.size) return component.html;
  let html = component.html;
  for (const [name, value] of request) {
    const declaration = declarations.get(name);
    if (!declaration) {
      throw new Error(
        `${component.id} has no state '${name}'. It declares: ${[...declarations.keys()].join(', ') || 'none'}.`);
    }
    html = declaration.kind === 'selection'
      ? applySelection(component.id, html, declaration, name, value)
      : applyFlag(component.id, html, declaration, name, value);
  }
  return html;
}

function applyFlag(id, html, declaration, name, value) {
  if (typeof value !== 'boolean') throw new Error(`${id} state '${name}' is a flag, so it takes no index.`);
  const root = openingTag(html);
  if (hasClass(root.attributes, declaration.modifier)) return html;
  const updated = addClass(root.attributes, declaration.modifier);
  return `${root.before}${updated}>${root.after}`;
}

function applySelection(id, html, declaration, name, value) {
  if (typeof value === 'boolean') throw new Error(`${id} state '${name}' is a selection, so it needs an index.`);
  const members = findMembers(html, declaration.member);
  if (!members.length) throw new Error(`${id} state '${name}' found no '.${declaration.member}' members.`);
  if (value >= members.length) {
    throw new Error(`${id} state '${name}' index ${value} is past the last of ${members.length} members.`);
  }
  // Rewrite from the last member backwards so earlier offsets stay valid.
  let updated = html;
  for (let index = members.length - 1; index >= 0; index -= 1) {
    const member = members[index];
    const selected = index === value;
    let attributes = selected
      ? addClass(member.attributes, declaration.modifier)
      : removeClass(member.attributes, declaration.modifier);
    attributes = setAriaState(attributes, selected);
    updated = `${updated.slice(0, member.start)}<${member.tag}${attributes}>${updated.slice(member.end)}`;
  }
  return updated;
}

/**
 * Members are matched on the class attribute rather than parsed, because the catalog's markup
 * is the contract: a regex that only ever edits one attribute cannot reorder or drop anything
 * else in it.
 */
function findMembers(html, member) {
  const members = [];
  const pattern = /<([a-z][a-z0-9]*)((?:[^>"']|"[^"]*"|'[^']*')*)>/gi;
  let match;
  while ((match = pattern.exec(html))) {
    if (!hasClass(match[2], member)) continue;
    members.push({
      tag: match[1],
      attributes: match[2],
      start: match.index,
      end: match.index + match[0].length,
    });
  }
  return members;
}

function openingTag(html) {
  const match = html.match(/^(\s*)<([a-z][a-z0-9]*)((?:[^>"']|"[^"]*"|'[^']*')*)>/i);
  if (!match) throw new Error('Component markup does not start with an element.');
  return {
    before: `${match[1]}<${match[2]}`,
    attributes: match[3],
    after: html.slice(match[0].length),
  };
}

function classList(attributes) {
  const match = attributes.match(/\sclass="([^"]*)"/);
  return match ? match[1].split(/\s+/).filter(Boolean) : [];
}

function hasClass(attributes, name) {
  return classList(attributes).includes(name);
}

/** Keeps the original leading whitespace, so only the class list itself changes. */
function writeClassList(attributes, names) {
  return attributes.replace(/(\s)class="[^"]*"/, (full, lead) => `${lead}class="${names.join(' ')}"`);
}

function addClass(attributes, name) {
  const names = classList(attributes);
  if (names.includes(name)) return attributes;
  return writeClassList(attributes, [...names, name]);
}

function removeClass(attributes, name) {
  const names = classList(attributes);
  if (!names.includes(name)) return attributes;
  return writeClassList(attributes, names.filter(entry => entry !== name));
}

/**
 * A selected tab that still says `aria-selected="false"` is a screenshot that lies to a
 * screen reader, so the aria attribute the catalog already carries follows the class.
 */
function setAriaState(attributes, selected) {
  let updated = attributes;
  for (const name of ['aria-selected', 'aria-pressed', 'aria-current']) {
    const pattern = new RegExp(`\\s${name}="[^"]*"`);
    if (!pattern.test(updated)) continue;
    updated = updated.replace(pattern, ` ${name}="${selected ? 'true' : 'false'}"`);
  }
  return updated;
}

/**
 * The documented examples of a component's states.
 *
 * The catalog has to show a reader what a selected tab looks like, and that used to mean a
 * second article with its own copy of the markup. Declaring the examples instead means the
 * markup is authored once and each example is generated from it, so the catalog documents
 * every state without the copies going stale against the component they illustrate.
 *
 *   data-au-state-examples="selected:2|Git tab selected|Working-tree review is primary here."
 *
 * Entries are separated by `;`, and each is `<state>|<title>|<note>`.
 */
export function parseStateExamples(raw) {
  const examples = [];
  for (const entry of (raw ?? '').split(';').map(value => value.trim()).filter(Boolean)) {
    const parts = entry.split('|').map(value => value.trim());
    if (parts.length !== 3 || parts.some(value => !value)) {
      throw new Error(`State example '${entry}' is not '<state>|<title>|<note>'.`);
    }
    examples.push({ state: parts[0], title: parts[1], note: parts[2] });
  }
  return examples;
}

export function buildStateExamples(component) {
  return parseStateExamples(component.stateExamples).map(example => ({
    state: example.state,
    title: example.title,
    note: example.note,
    html: applyStates(component, example.state),
  }));
}

/**
 * Neutralises the classes a scene is allowed to have moved, and the aria attributes that track
 * them, so a parameterised scene can still be compared against its catalog component verbatim
 * for everything else. `ScreenshotAppContractProvider.NormaliseStates` is the same rule in C#;
 * `Verify_repositoryScenesComposeCatalog` is what holds the two to the same answer.
 */
export function normaliseStates(html, modifiers) {
  const flattened = html.replace(/(aria-selected|aria-pressed|aria-current)="[^"]*"/g, '$1="\u0000"');
  if (!modifiers?.length) return flattened;
  const drop = new Set(modifiers);
  return flattened.replace(/class="([^"]*)"/g, (full, names) => {
    const kept = names.split(' ').filter(name => name && !drop.has(name));
    return `class="${kept.join(' ')}"`;
  });
}
