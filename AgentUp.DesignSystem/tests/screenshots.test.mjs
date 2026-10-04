import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import catalog from '../dist/web/catalog.json' with { type: 'json' };
import {
  assembleScreens,
  desktopSize,
  framedSceneHtml,
  isLayoutShell,
  mobileSize,
  sceneStylesheetHref,
  wrapSceneDocument,
} from '../scripts/lib/screens.mjs';
import { normaliseStates } from '../scripts/lib/states.mjs';

const repository = fileURLToPath(new URL('../..', import.meta.url));
const css = await readFile(new URL('../dist/web/screenshots.css', import.meta.url), 'utf8');
const screensHtml = await readFile(new URL('../src/screens.html', import.meta.url), 'utf8');
const assembled = assembleScreens(catalog, screensHtml);
const components = Object.fromEntries(
  catalog.surfaces.flatMap(surface => surface.components.map(component => [component.id, component])),
);
const allowed = new Set([...css.matchAll(/\.((?:au-[a-z0-9-]+))/g)].map(match => match[1]));
const requiredViews = {
  desktop: ['sign-in', 'workspaces', 'applications', 'console', 'git', 'history', 'agents', 'diagnostics', 'metrics', 'validation', 'database', 'capabilities', 'file-viewer'],
  mobile: ['sign-in', 'workspaces', 'apps', 'git', 'review', 'history', 'agents', 'settings', 'file-viewer', 'agent', 'application', 'workspace-list'],
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
    assert.equal(html, wrapSceneDocument(scene).match(/<body class="au-theme">\n([\s\S]*)\n<\/body>/)?.[1]);
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
    const html = wrapSceneDocument(scene);
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
      // A component the scene parameterised differs from its catalog example: a state moved a
      // declared modifier, which is neutralised on both sides, and a prop replaced copy, which
      // cannot be re-derived, so the generator recorded the resolved fragment to compare with.
      const expected = scene.componentFragments[id] ?? component.html;
      const sceneHtml = normaliseStates(html, scene.stateModifiers);
      if (!isLayoutShell(expected) && !sceneHtml.includes(normaliseStates(expected, scene.stateModifiers))) {
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
  assert.doesNotMatch(source, /Harbor Mug|Harbor Shop|ProductGrid\.tsx|http:\/\/127\.0\.0\.1:9/);
  assert.doesNotMatch(source, /class="au-screen"/);
  assert.doesNotMatch(source, /class="au-chrome"/);
  assert.doesNotMatch(source, /class="au-git-row"/);
  assert.doesNotMatch(source, /class="au-sign-in"/);
});

test('assembled Demo scenes insert Harbor Shop catalog HTML', () => {
  const git = assembled.scenes.find(scene => scene.id === 'desktop-git');
  const apps = assembled.scenes.find(scene => scene.id === 'desktop-applications');
  const signIn = assembled.scenes.find(scene => scene.id === 'desktop-sign-in');
  assert.ok(git.html.includes('ProductGrid.tsx'));
  assert.ok(git.html.includes(components['git-change-list'].html));
  assert.ok(apps.html.includes('Harbor Mug'));
  assert.ok(apps.html.includes(components.storefront.html));
  assert.ok(signIn.html.includes('http://127.0.0.1:9'));
  assert.ok(signIn.html.includes(components['chrome-connect'].html));
  assert.ok(!signIn.html.includes('☰'));
  const mobileViewer = assembled.scenes.find(scene => scene.id === 'mobile-file-viewer');
  assert.ok(mobileViewer.html.includes(components['file-viewer-mobile'].html));
  assert.ok(!mobileViewer.html.includes('>Go</button>'));
  const workspaces = assembled.scenes.find(scene => scene.id === 'mobile-workspaces');
  assert.ok(workspaces.html.includes(components['app-list'].html));
  assert.ok(workspaces.html.includes(components.drawer.html));
  assert.ok(!workspaces.html.includes(components['capability-settings'].html));
  const mobileSignIn = assembled.scenes.find(scene => scene.id === 'mobile-sign-in');
  assert.ok(!mobileSignIn.html.includes('autofocus'));
  const mobileGit = assembled.scenes.find(scene => scene.id === 'mobile-git');
  assert.ok(mobileGit.html.includes('<textarea class="au-input"'));
  assert.ok(!mobileGit.html.includes('autofocus'));
  const validation = assembled.scenes.find(scene => scene.id === 'desktop-validation');
  assert.ok(validation.html.includes(components.storefront.html));
  assert.ok(validation.html.includes(components['validation-sidebar'].html));
  assert.ok(validation.html.includes('au-screen-aside'));
  assert.ok(validation.html.includes('au-validation-check'));
  assert.ok(!validation.components.includes('validation-stage'));
});
