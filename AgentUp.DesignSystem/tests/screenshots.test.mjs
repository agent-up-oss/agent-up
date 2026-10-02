import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import test from 'node:test';
import catalog from '../dist/web/catalog.json' with { type: 'json' };
import {
  assembleScreens,
  desktopSize,
  framedSceneHtml,
  isLayoutShell,
  mobileSize,
  wrapSceneDocument,
} from '../scripts/lib/screens.mjs';

const repository = resolve(new URL('../..', import.meta.url).pathname);
const css = await readFile(new URL('../dist/web/screenshots.css', import.meta.url), 'utf8');
const screensHtml = await readFile(new URL('../src/screens.html', import.meta.url), 'utf8');
const assembled = assembleScreens(catalog, screensHtml);
const components = Object.fromEntries(
  catalog.surfaces.flatMap(surface => surface.components.map(component => [component.id, component])),
);
const allowed = new Set([...css.matchAll(/\.((?:au-[a-z0-9-]+))/g)].map(match => match[1]));
const requiredViews = {
  desktop: ['sign-in', 'workspaces', 'applications', 'console', 'git', 'history', 'agents', 'diagnostics', 'metrics', 'validation', 'database', 'capabilities', 'file-viewer'],
  mobile: ['sign-in', 'workspaces', 'apps', 'git', 'review', 'history', 'agents', 'settings', 'file-viewer'],
};

test('assembled screens pair every scene for the showcase', () => {
  const { scenes, screens } = assembled;
  const used = [];
  const ids = new Set(scenes.map(scene => scene.id));
  assert.equal(new Set(screens.map(screen => screen.id)).size, screens.length);
  for (const screen of screens) {
    assert.ok(screen.title, `${screen.id} is missing a title`);
    assert.ok(screen.intro, `${screen.id} is missing an intro`);
    assert.ok(screen.desktopId || screen.mobileId, `${screen.id} has no assembled scene`);
    for (const sceneId of [screen.desktopId, screen.mobileId].filter(Boolean)) {
      assert.ok(ids.has(sceneId), `${screen.id} points at missing scene ${sceneId}`);
      used.push(sceneId);
    }
  }
  assert.equal(new Set(used).size, used.length, 'assembled screens reuse a scene');
  assert.deepEqual([...used].sort(), [...ids].sort(), 'assembled screens drifted from scenes');
});

test('framed screen HTML is a catalog fragment the showcase can mount', () => {
  for (const scene of assembled.scenes) {
    const html = framedSceneHtml(scene);
    assert.match(html, /au-screen/);
    assert.doesNotMatch(html, /au-screenshot-/);
    assert.doesNotMatch(html, /<!DOCTYPE html>/);
    assert.equal(html, wrapSceneDocument(scene, css).match(/<body class="au-theme">\n([\s\S]*)\n<\/body>/)?.[1]);
  }
});

test('assembled scenes cover every major Desktop and Mobile view once', () => {
  const scenes = assembled.scenes;
  const ids = scenes.map(scene => scene.id);
  assert.equal(new Set(ids).size, ids.length);
  assert.equal(scenes.filter(scene => scene.hero).length, 1);
  assert.equal(scenes.find(scene => scene.hero)?.id, 'desktop-applications');
  for (const [surface, views] of Object.entries(requiredViews)) {
    const got = scenes.filter(scene => scene.surface === surface).map(scene => scene.view);
    assert.deepEqual(got, views, `${surface} assembled views drifted`);
  }
});

test('assembled screens insert catalog component HTML instead of restating it', () => {
  const missing = [];
  for (const scene of assembled.scenes) {
    const html = wrapSceneDocument(scene, css);
    assert.ok(scene.components.length > 0, `${scene.id} uses no catalog components`);
    const classes = [...html.matchAll(/class="([^"]+)"/g)]
      .flatMap(match => match[1].split(/\s+/))
      .filter(Boolean);
    for (const name of classes) {
      if (!allowed.has(name)) missing.push(`${scene.id} unknown class ${name}`);
    }
    for (const id of scene.components) {
      const component = components[id];
      if (!component) {
        missing.push(`${scene.id} unknown component ${id}`);
        continue;
      }
      if (!isLayoutShell(component.html) && !html.includes(component.html)) {
        missing.push(`${scene.id} missing catalog HTML for ${id}`);
      }
      if (!classes.includes(component.rootClass)) missing.push(`${scene.id} missing class ${component.rootClass}`);
    }
    const size = scene.surface === 'mobile' ? mobileSize : desktopSize;
    assert.equal(scene.width, size.width, scene.id);
    assert.equal(scene.height, size.height, scene.id);
  }
  assert.deepEqual(missing, [], 'assembled screens drifted from the catalog');
});

test('assembled screen source does not duplicate catalog component markup', async () => {
  const source = await readFile(resolve(repository, 'AgentUp.DesignSystem/src/screens.html'), 'utf8');
  assert.match(source, /data-au-use="sign-in"/);
  assert.match(source, /data-au-use="screen"/);
  assert.match(source, /data-au-layout="desktop-shell"/);
  assert.doesNotMatch(source, /Harbor Mug|ProductGrid\.tsx|http:\/\/127\.0\.0\.1:9/);
  assert.doesNotMatch(source, /class="au-screen"/);
  assert.doesNotMatch(source, /class="au-chrome"/);
  assert.doesNotMatch(source, /class="au-git-row"/);
  assert.doesNotMatch(source, /class="au-sign-in"/);
});
