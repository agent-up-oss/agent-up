import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import test from 'node:test';
import { desktopSize, mobileSize, screenshotScenes, wrapSceneDocument } from '../scripts/lib/screenshots.mjs';

const repository = resolve(new URL('../..', import.meta.url).pathname);
const css = await readFile(new URL('../dist/web/screenshots.css', import.meta.url), 'utf8');
const definition = await readFile(resolve(repository, 'AgentUp.FakeServer/definition.json'), 'utf8');
const allowed = new Set([...css.matchAll(/\.((?:au-[a-z0-9-]+))/g)].map(match => match[1]));
const requiredViews = {
  desktop: ['sign-in', 'workspaces', 'applications', 'console', 'git', 'history', 'agents', 'diagnostics', 'metrics', 'validation', 'database', 'capabilities', 'file-viewer'],
  mobile: ['sign-in', 'workspaces', 'apps', 'git', 'review', 'history', 'agents', 'settings', 'file-viewer'],
};

test('screenshot scenes cover every major Desktop and Mobile view once', () => {
  const scenes = screenshotScenes();
  const ids = scenes.map(scene => scene.id);
  assert.equal(new Set(ids).size, ids.length);
  assert.equal(scenes.filter(scene => scene.hero).length, 1);
  assert.equal(scenes.find(scene => scene.hero)?.id, 'desktop-applications');
  for (const [surface, views] of Object.entries(requiredViews)) {
    const got = scenes.filter(scene => scene.surface === surface).map(scene => scene.view);
    assert.deepEqual(got, views, `${surface} screenshot views drifted`);
  }
});

test('screenshot HTML uses catalog classes, FakeServer copy, and the classes the apps ship', async () => {
  const missing = [];
  for (const scene of screenshotScenes()) {
    const html = wrapSceneDocument(scene, css);
    const classes = [...html.matchAll(/class="([^"]+)"/g)]
      .flatMap(match => match[1].split(/\s+/))
      .filter(Boolean);
    for (const name of classes) {
      if (name.startsWith('au-screenshot')) continue;
      if (!allowed.has(name)) missing.push(`${scene.id} unknown class ${name}`);
    }
    for (const name of scene.requiredClasses) {
      if (!classes.includes(name)) missing.push(`${scene.id} missing class ${name}`);
    }
    const sources = await Promise.all(scene.appSources.map(path => readFile(resolve(repository, path), 'utf8')));
    const joined = sources.join('\n');
    for (const name of scene.requiredDesktopClasses) {
      if (!joined.includes(name)) missing.push(`${scene.id} missing Desktop class ${name}`);
    }
    for (const name of scene.requiredMobileComponents) {
      if (!joined.includes(`auBox('${name}'`) && !joined.includes(`auBox("${name}"`)) {
        missing.push(`${scene.id} missing auBox('${name}')`);
      }
    }
    for (const copy of scene.copy) {
      if (!html.includes(copy)) missing.push(`${scene.id} HTML missing copy ${copy}`);
      if (!definition.includes(copy)) missing.push(`${scene.id} FakeServer missing copy ${copy}`);
    }
    const size = scene.surface === 'mobile' ? mobileSize : desktopSize;
    assert.equal(scene.width, size.width, scene.id);
    assert.equal(scene.height, size.height, scene.id);
  }
  assert.deepEqual(missing, [], 'screenshot scenes drifted from the catalog or the apps');
});
