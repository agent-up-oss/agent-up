import { parseScreens, slottedScene } from './screens.mjs';

/**
 * Which catalog components each screen is built from, for the showcase.
 *
 * A screen names components in its layout and regions, and a component can name others inside
 * itself (`data-au-use`, `data-au-repeat`). The showcase lists the whole set under the screen
 * previews, so a reader sees exactly what goes where. One UI's catalog is read on its own: the
 * Old UI and the New UI share no component definitions.
 */
export function screenComponents(screensHtml, catalogHtml, { renderedScenes = new Map(), components = [] } = {}) {
  const uses = componentUses(catalogHtml);
  const named = new Set([...namedIn(catalogHtml), ...namedIn(screensHtml)]);
  const { layouts, screens, scenes } = parseScreens(screensHtml);
  const sceneById = new Map(scenes.map(scene => [scene.id, scene]));
  // Only a class one component owns says which component is drawn; a shared one (a pane, a tab)
  // would list every component that happens to reuse it.
  const owners = new Map();
  for (const component of components) {
    if (component.rootClass) owners.set(component.rootClass, (owners.get(component.rootClass) ?? 0) + 1);
  }
  const distinct = new Set([...owners].filter(([, count]) => count === 1).map(([name]) => name));
  const result = new Map();
  for (const screen of screens) {
    const found = new Set();
    for (const sceneId of [screen.desktopId, screen.mobileId]) {
      const scene = sceneById.get(sceneId);
      if (!scene) continue;
      collect(namedIn(slottedScene(scene, layouts)), uses, found);
    }
    // A part no screen or component names (a chrome button, a Git tree row) is listed wherever
    // its class is drawn. A component that is named somewhere is listed only where it is named, so
    // a class it shares (the phone tab bar's buttons reuse the subtab class) does not drag it in.
    const drawn = new Set([screen.desktopId, screen.mobileId]
      .flatMap(sceneId => classesIn(renderedScenes.get(sceneId) ?? '')));
    for (const component of components) {
      if (named.has(component.id) || !component.rootClass || !distinct.has(component.rootClass)) continue;
      if (drawn.has(component.rootClass)) found.add(component.id);
    }
    result.set(screen.id, [...found].filter(id => uses.has(id)));
  }
  return result;
}

/** Each component id and the components its own markup names. */
export function componentUses(catalogHtml) {
  const uses = new Map();
  const article = /<article\b[^>]*data-au-component="([^"]+)"[^>]*>([\s\S]*?)<\/article>/g;
  let match;
  while ((match = article.exec(catalogHtml))) uses.set(match[1], namedIn(match[2]));
  return uses;
}

function classesIn(html) {
  return [...html.matchAll(/class="([^"]+)"/g)].flatMap(match => match[1].split(/\s+/));
}

function namedIn(html) {
  return [...html.matchAll(/data-au-(?:use|repeat)="([^"]+)"/g)].map(match => match[1]);
}

function collect(ids, uses, found) {
  for (const id of ids) {
    if (found.has(id)) continue;
    found.add(id);
    collect(uses.get(id) ?? [], uses, found);
  }
}
