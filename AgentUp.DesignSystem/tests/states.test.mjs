import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import catalog from '../dist/web/catalog.json' with { type: 'json' };
import { assembleScreens } from '../scripts/lib/screens.mjs';
import {
  applyStates,
  buildStateExamples,
  declaredModifiers,
  normaliseStates,
  parseStateDeclarations,
  parseStateExamples,
  parseStateRequest,
} from '../scripts/lib/states.mjs';

const screensHtml = await readFile(new URL('../src/screens.html', import.meta.url), 'utf8');
const assembled = assembleScreens(catalog, screensHtml);
const components = Object.fromEntries(
  catalog.surfaces.flatMap(surface => surface.components.map(component => [component.id, component])),
);

const tabs = {
  id: 'tabs',
  states: 'selected=au-tab/au-tab--selected',
  html: '<div class="au-cluster">'
    + '<button class="au-tab au-tab--selected" aria-selected="true">Overview</button>'
    + '<button class="au-tab" aria-selected="false">Git</button>'
    + '</div>',
};

const sidebar = {
  id: 'sidebar',
  states: 'collapsed=au-sidebar--collapsed',
  html: '<aside class="au-sidebar"><p>Validation</p></aside>',
};

test('a selection moves the modifier and the aria state together', () => {
  const html = applyStates(tabs, 'selected:1');

  assert.match(html, /<button class="au-tab" aria-selected="false">Overview<\/button>/);
  assert.match(html, /<button class="au-tab au-tab--selected" aria-selected="true">Git<\/button>/);
});

test('a selection changes nothing but the declared modifier and its aria state', () => {
  const modifiers = declaredModifiers(parseStateDeclarations(tabs.states));

  assert.equal(normaliseStates(applyStates(tabs, 'selected:1'), modifiers), normaliseStates(tabs.html, modifiers));
});

test('a flag adds the declared modifier to the component root', () => {
  assert.equal(
    applyStates(sidebar, 'collapsed'),
    '<aside class="au-sidebar au-sidebar--collapsed"><p>Validation</p></aside>');
});

test('an undeclared state fails the build rather than rendering unchanged', () => {
  assert.throws(() => applyStates(tabs, 'expanded'), /has no state 'expanded'/);
});

test('a selection index past the last member fails the build', () => {
  assert.throws(() => applyStates(tabs, 'selected:9'), /index 9 is past the last of 2 members/);
});

test('a selection needs an index and a flag must not carry one', () => {
  assert.throws(() => applyStates(tabs, 'selected'), /needs an index/);
  assert.throws(() => applyStates(sidebar, 'collapsed:1'), /takes no index/);
});

test('a malformed declaration or request is rejected', () => {
  assert.throws(() => parseStateDeclarations('selected'), /is not '<name>=<spec>'/);
  assert.throws(() => parseStateDeclarations('selected=au-tab/'), /needs '<member>\/<modifier>'/);
  assert.throws(() => parseStateDeclarations('a=x b=y a=z'), /declared twice/);
  assert.throws(() => parseStateRequest('selected:last'), /needs a zero-based index/);
  assert.throws(() => parseStateExamples('selected:1|only-a-title'), /is not '<state>\|<title>\|<note>'/);
});

test('documented state examples are generated from the component markup', () => {
  const examples = buildStateExamples({ ...tabs, stateExamples: 'selected:1|Git selected|Git is open.' });

  assert.equal(examples.length, 1);
  assert.equal(examples[0].title, 'Git selected');
  assert.equal(examples[0].html, applyStates(tabs, 'selected:1'));
});

test('every catalog state declaration resolves against its own markup', () => {
  for (const component of Object.values(components)) {
    for (const example of component.stateExamples ?? []) {
      assert.equal(typeof example.html, 'string');
      assert.ok(example.html.length > 0, `${component.id} example ${example.state} is empty`);
    }
  }
});

test('a scene names the catalog component plus a state instead of a copy of it', () => {
  const retired = [
    'workspace-tabs-applications', 'workspace-tabs-git', 'workspace-tabs-agent',
    'mobile-tab-bar-git', 'mobile-tab-bar-agents', 'mobile-tab-bar-settings',
    'subtab-console', 'git-row-selected', 'page-jump-current',
  ];
  for (const id of retired) {
    assert.ok(!(id in components), `${id} is back in the catalog as a copy of a state`);
    assert.ok(!screensHtml.includes(`data-au-use="${id}"`), `a scene still uses ${id}`);
  }
});

test('each application sub-tab scene selects the sub-tab it shows', () => {
  const selected = id => {
    const scene = assembled.scenes.find(entry => entry.id === id);
    const match = scene.html.match(/<button class="au-subtab au-subtab--selected"[^>]*>([\s\S]*?)<\/button>/);
    return match?.[1].replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim();
  };

  assert.equal(selected('desktop-console'), 'Console');
  assert.equal(selected('desktop-diagnostics'), 'Diagnostics');
  assert.equal(selected('desktop-metrics'), 'Metrics');
  assert.equal(selected('desktop-database'), 'Database');
});

test('the screen shell and the Demo fixtures stay out of the product bindings', async () => {
  const { readFile } = await import('node:fs/promises');
  const avalonia = await readFile(new URL('../dist/avalonia/AgentUpStyles.axaml', import.meta.url), 'utf8');
  const native = await import('../dist/native/index.js');
  const nativeKeys = Object.keys(native.agentUpTheme.components);

  for (const className of (await import('../scripts/lib/css.mjs')).webOnly) {
    assert.ok(
      !avalonia.includes(`.${className}"`),
      `${className} reached Avalonia; Desktop composes its shell from Grid and cannot use it.`);
    const key = className.replace(/^au-/, '').replace(/-([a-z0-9])/g, (full, c) => c.toUpperCase());
    assert.ok(!nativeKeys.includes(key), `${className} reached the React Native bundle as ${key}.`);
  }
});

test('every Avalonia alias a scene retires is one Desktop does not reference', async () => {
  const { readFile } = await import('node:fs/promises');
  const desktop = await readFile(new URL('../../AgentUp.Desktop/Features/Workspaces/Views/MainWindow.axaml', import.meta.url), 'utf8');
  const avalonia = await readFile(new URL('../dist/avalonia/AgentUpStyles.axaml', import.meta.url), 'utf8');

  // Desktop names a class two ways while it carries both vocabularies.
  const referenced = new Set([
    ...[...desktop.matchAll(/Classes="([^"]+)"/g)].flatMap(match => match[1].split(/\s+/)).filter(Boolean),
    ...[...desktop.matchAll(/Classes\.([A-Za-z0-9_-]+)\s*=/g)].map(match => match[1]),
  ]);
  const emitted = new Set([...avalonia.matchAll(/Selector="(?:Border|TextBlock)\.([A-Za-z0-9_-]+)"/g)].map(match => match[1]));

  for (const alias of ['gitNodeRow', 'gitNodeRowSelected', 'auditPageJumpInner', 'auditPageJumpInnerCurrent']) {
    assert.ok(referenced.has(alias), `Desktop no longer references ${alias}; drop the mapping instead of keeping it.`);
    assert.ok(emitted.has(alias), `${alias} lost its generated style, so Desktop's binding paints nothing.`);
  }
});
