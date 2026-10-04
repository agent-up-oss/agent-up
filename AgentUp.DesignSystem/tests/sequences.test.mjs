import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import catalog from '../dist/web/catalog.json' with { type: 'json' };
import manifest from '../dist/web/screenshots.json' with { type: 'json' };
import { assembleScreens } from '../scripts/lib/screens.mjs';
import { buildSequence, parseSequence } from '../scripts/lib/sequences.mjs';
import { applySequenceStep, playSequence, seekSequence } from '../dist/web/sequence.js';

const screensHtml = await readFile(new URL('../src/screens.html', import.meta.url), 'utf8');
const assembled = assembleScreens(catalog, screensHtml);
const components = new Map(
  catalog.surfaces.flatMap(surface => surface.components.map(component => [component.id, component])),
);

/**
 * The smallest element the player actually touches: a class list, the aria attributes that
 * track it, and a query by class. Standing a real DOM up for this would test jsdom.
 */
function fakeScene(memberClass, count) {
  const members = Array.from({ length: count }, () => {
    const classes = new Set([memberClass]);
    const attributes = new Map([['aria-selected', 'false']]);
    return {
      classList: {
        toggle(name, on) { if (on) classes.add(name); else classes.delete(name); },
        contains(name) { return classes.has(name); },
      },
      hasAttribute: name => attributes.has(name),
      setAttribute: (name, value) => attributes.set(name, value),
      getAttribute: name => attributes.get(name),
      classes,
    };
  });
  const rootClasses = new Set();
  return {
    members,
    classList: {
      toggle(name, on) { if (on) rootClasses.add(name); else rootClasses.delete(name); },
      contains(name) { return rootClasses.has(name); },
    },
    querySelectorAll(selector) {
      assert.equal(selector, `.${memberClass}`);
      return members;
    },
  };
}

test('a step moves the modifier and the aria state without replacing an element', () => {
  const root = fakeScene('au-app-tab', 4);
  const before = root.members.slice();

  applySequenceStep(root, { apply: { kind: 'selection', member: 'au-app-tab', modifier: 'au-app-tab--selected', index: 2 } });

  assert.deepEqual(root.members, before, 'the player replaced an element, so a transition cannot run');
  assert.deepEqual(
    root.members.map(member => member.classList.contains('au-app-tab--selected')),
    [false, false, true, false]);
  assert.deepEqual(root.members.map(member => member.getAttribute('aria-selected')), ['false', 'false', 'true', 'false']);
});

test('a flag step toggles the modifier on the scene root', () => {
  const root = fakeScene('au-app-tab', 1);

  applySequenceStep(root, { apply: { kind: 'flag', modifier: 'au-validation-sidebar--collapsed', on: true } });
  assert.ok(root.classList.contains('au-validation-sidebar--collapsed'));

  applySequenceStep(root, { apply: { kind: 'flag', modifier: 'au-validation-sidebar--collapsed', on: false } });
  assert.ok(!root.classList.contains('au-validation-sidebar--collapsed'));
});

test('seeking returns the hold so a frame grabber can spread frames over real time', () => {
  const root = fakeScene('au-app-tab', 4);
  const sequence = manifest.scenes.find(scene => scene.id === 'desktop-workspaces').sequence;

  assert.equal(seekSequence(root, sequence, 1), sequence[1].hold);
  assert.ok(root.members[1].classList.contains('au-app-tab--selected'));
  assert.equal(seekSequence(root, sequence, 99), 0, 'seeking past the end holds still');
});

test('playing advances through every frame and the stop function cancels', () => {
  const root = fakeScene('au-app-tab', 4);
  const sequence = manifest.scenes.find(scene => scene.id === 'desktop-workspaces').sequence;
  const pending = [];
  let cancelled = 0;
  const stop = playSequence(root, sequence, {
    reducedMotion: false,
    setTimeout: (fn, ms) => { pending.push({ fn, ms }); return pending.length; },
    clearTimeout: () => { cancelled += 1; },
  });

  const seen = [selectedIndex(root)];
  for (let step = 0; step < sequence.length; step += 1) {
    const next = pending.shift();
    assert.ok(next, 'the player stopped scheduling before the sequence looped');
    next.fn();
    seen.push(selectedIndex(root));
  }
  stop();

  assert.deepEqual(seen.slice(0, sequence.length), sequence.map(frame => frame.apply.index));
  assert.equal(seen.at(-1), sequence[0].apply.index, 'the sequence loops back to its first frame');
  assert.equal(cancelled, 1);
});

test('reduced motion holds the first frame instead of animating', () => {
  const root = fakeScene('au-app-tab', 4);
  const sequence = manifest.scenes.find(scene => scene.id === 'desktop-workspaces').sequence;
  let scheduled = 0;

  playSequence(root, sequence, { reducedMotion: true, setTimeout: () => { scheduled += 1; } });

  assert.equal(scheduled, 0);
  assert.equal(selectedIndex(root), sequence[0].apply.index);
});

test('a sequence step is resolved against the component it names', () => {
  assert.deepEqual(
    parseSequence('workspace-tabs=selected:2@900'),
    [{ component: 'workspace-tabs', state: 'selected:2', hold: 900 }]);
  assert.throws(() => parseSequence('workspace-tabs=selected:2@0'), /positive hold/);
  assert.throws(() => parseSequence('workspace-tabs@900'), /is not '<component>=<state>@<hold>'/);
});

test('a sequence naming an unused component or an undeclared state fails the build', () => {
  const scene = { id: 'desktop-workspaces', components: ['workspace-tabs'] };
  assert.throws(
    () => buildSequence(scene, 'chrome=selected:0@900', components),
    /sequences 'chrome', which the scene does not use/);
  assert.throws(
    () => buildSequence(scene, 'workspace-tabs=expanded@900', components),
    /sequences state 'expanded'/);
});

test('every declared sequence resolves and names a state the component has', () => {
  const sequenced = assembled.scenes.filter(scene => scene.sequence.length);

  assert.ok(sequenced.length >= 2, 'no scene demonstrates a sequence');
  for (const scene of sequenced) {
    for (const frame of scene.sequence) {
      assert.ok(scene.components.includes(frame.component));
      assert.ok(frame.hold > 0);
      assert.ok(frame.apply.modifier.includes('--'));
      assert.ok(scene.stateModifiers.includes(frame.apply.modifier),
        `${scene.id} sequences ${frame.apply.modifier} without declaring it as a state modifier`);
    }
  }
});

function selectedIndex(root) {
  return root.members.findIndex(member => [...member.classes].some(name => name.endsWith('--selected')));
}
