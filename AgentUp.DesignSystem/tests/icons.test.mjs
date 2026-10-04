import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import catalog from '../dist/web/catalog.json' with { type: 'json' };
import iconManifest from '../dist/web/icons.json' with { type: 'json' };
import { emitAvaloniaIcons, emitNativeIcons, expandIcons, iconSvg, parseIcons } from '../scripts/lib/icons.mjs';

const source = await readFile(new URL('../src/icons.html', import.meta.url), 'utf8');
const icons = parseIcons(source);
const catalogSource = await readFile(new URL('../src/catalog.html', import.meta.url), 'utf8');
const screensSource = await readFile(new URL('../src/screens.html', import.meta.url), 'utf8');

test('the geometry is declared once and referenced everywhere else', () => {
  assert.equal(catalogSource.includes('<svg'), false, 'the catalog inlines an icon instead of naming one');
  assert.equal(screensSource.includes('<svg'), false, 'a screen inlines an icon instead of naming one');
});

test('every icon reference in source names a declared icon', () => {
  const declared = new Set(icons.map(icon => icon.id));
  for (const html of [catalogSource, screensSource]) {
    for (const match of html.matchAll(/data-au-icon="([^"]+)"/g)) {
      assert.ok(declared.has(match[1]), `undeclared icon ${match[1]}`);
    }
  }
});

test('an unknown icon reference fails the build', () => {
  assert.throws(() => expandIcons('<span data-au-icon="nope"></span>', icons), /Unknown icon 'nope'/);
});

test('each surface gets the same icon set from the one declaration', () => {
  const ids = icons.map(icon => icon.id);
  const avalonia = emitAvaloniaIcons(icons);
  const native = emitNativeIcons(icons);

  assert.deepEqual(iconManifest.icons.map(icon => icon.id), ids);
  for (const id of ids) {
    assert.ok(avalonia.includes(`{ "${id}",`), `${id} is missing from the Avalonia icons`);
    assert.ok(native.includes(`"${id}":`), `${id} is missing from the React Native icons`);
  }
});

test('emitted path data is the subset Avalonia and react-native-svg accept', () => {
  for (const icon of icons) {
    assert.ok(icon.paths.length > 0, `${icon.id} emitted no path`);
    for (const path of icon.paths) {
      assert.match(path, /^M/, `${icon.id} path does not start with a move`);
      assert.doesNotMatch(path, /[^MmLlHhVvCcSsQqTtAaZz0-9.,\s-]/, `${icon.id} path uses an unsupported command`);
    }
  }
});

test('a rect and a circle are converted rather than passed through', () => {
  const apps = icons.find(icon => icon.id === 'apps');
  const git = icons.find(icon => icon.id === 'git');

  assert.equal(apps.paths.length, 4, 'the four app squares each become a path');
  assert.ok(apps.paths.every(path => path.includes(' A ')), 'a rounded rect keeps its corner arcs');
  assert.ok(git.paths.filter(path => path.startsWith('M 3.5')).length > 0, 'a circle becomes an arc pair');
});

test('the catalog renders the declared drawing for each reference', () => {
  const menu = icons.find(icon => icon.id === 'menu');
  const component = catalog.surfaces
    .flatMap(surface => surface.components)
    .find(entry => entry.id === 'mobile-bar');

  assert.ok(component.html.includes(iconSvg(menu)), 'the mobile bar does not carry the declared menu icon');
});

test('every declared icon carries the copy the catalog documents it with', () => {
  for (const icon of icons) {
    assert.ok(icon.title.length > 0, `${icon.id} has no title`);
    assert.ok(icon.note.length > 0, `${icon.id} has no note`);
  }
});
