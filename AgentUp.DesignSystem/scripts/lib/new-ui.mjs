/**
 * The New UI space.
 *
 * A second arrangement of the same design tokens, kept entirely out of the compiled product
 * pipeline. Nothing here reaches `parseCatalog`, `parseRules`, `emitNative` or `emitStyles`, so
 * adding or changing a scene cannot move a single byte in the Avalonia resources, the React
 * Native bindings or `dist/web/agent-up.css` that the shipping clients consume.
 *
 * Scenes are authored in `src/new-ui/screens.html` as plain markup. A `<template
 * data-aun-partial>` is inserted wherever a scene writes `<div data-aun-include>`, so the rail
 * and the agent header are written once rather than copied into every screen.
 */

export const newUiDesktopSize = { width: 1440, height: 900 };
export const newUiMobileSize = { width: 390, height: 844 };

const stylesheetHref = '../new-ui.css';

export function parseNewUi(html) {
  const partials = collectPartials(html);
  const scenes = collectScenes(html).map(scene => ({
    ...scene,
    html: applyIncludes(scene.html, partials),
  }));
  if (!scenes.length) throw new Error('The New UI space has no scenes.');
  const seen = new Set();
  for (const scene of scenes) {
    // The tag match no longer insists on a non-empty id, so the check belongs here instead.
    if (!scene.id) throw new Error('A New UI scene declares data-aun-scene with no id.');
    if (seen.has(scene.id)) throw new Error(`Duplicate New UI scene id "${scene.id}".`);
    seen.add(scene.id);
    if (scene.surface !== 'desktop' && scene.surface !== 'mobile') {
      throw new Error(`New UI scene "${scene.id}" must declare data-aun-surface of desktop or mobile.`);
    }
    if (!scene.title) throw new Error(`New UI scene "${scene.id}" is missing data-aun-title.`);
    if (/data-aun-include/.test(scene.html)) {
      throw new Error(`New UI scene "${scene.id}" includes a partial that does not exist.`);
    }
  }
  return scenes;
}

export function newUiSceneSize(scene) {
  return scene.surface === 'mobile' ? newUiMobileSize : newUiDesktopSize;
}

export function newUiManifest(scenes) {
  return {
    space: 'new-ui',
    stylesheet: 'new-ui.css',
    scenes: scenes.map(scene => ({
      id: scene.id,
      title: scene.title,
      surface: scene.surface,
      intro: scene.intro,
      htmlFile: `new-ui/${scene.id}.html`,
      width: newUiSceneSize(scene).width,
      height: newUiSceneSize(scene).height,
      html: scene.html,
    })),
  };
}

/**
 * One scene as a standalone page, sized to its surface so a screenshot of it is the screenshot
 * of that surface rather than of a browser window that happened to be open.
 */
export function wrapNewUiScene(scene) {
  const { width, height } = newUiSceneSize(scene);
  const frame = scene.surface === 'mobile'
    ? `width: ${width}px; height: ${height}px;`
    : `width: ${width}px; min-height: ${height}px;`;
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>${escapeText(stripMarkup(scene.title))} — Agent-Up new UI</title>
  <link rel="stylesheet" href="${stylesheetHref}" />
  <style>
    html, body { margin: 0; padding: 0; background: var(--au-color-canvas); }
    .aun-frame { ${frame} overflow: hidden; display: flex; }
    .aun-frame > * { flex: 1 1 auto; min-width: 0; }
  </style>
</head>
<body>
<div class="aun-frame">
${scene.html}
</div>
</body>
</html>
`;
}

function collectPartials(html) {
  const partials = new Map();
  const pattern = /<template\s+data-aun-partial="([^"]+)"\s*>([\s\S]*?)<\/template>/g;
  let match;
  while ((match = pattern.exec(html)) !== null) partials.set(match[1], match[2].trim());
  return partials;
}

/**
 * Scenes contain their own <section> elements, so the closing tag has to be found by counting
 * depth rather than by matching the first one. A non-greedy regex silently truncated every
 * scene at its first inner </section>, which dropped whole regions off the end of a screen.
 */
function collectScenes(html) {
  const scenes = [];
  // One unbounded run, not two around a literal: `[^>]*?data-aun-scene="..."[^>]*?` backtracks
  // quadratically over any long tag that turns out not to be a scene. Match the attributes once
  // and ask afterwards whether they name a scene.
  const open = /<section\s+([^>]*)>/g;
  let match;
  while ((match = open.exec(html)) !== null) {
    const attributes = match[1];
    if (!attributes.includes('data-aun-scene=')) continue;
    const bodyStart = match.index + match[0].length;
    const bodyEnd = findSectionClose(html, bodyStart);
    scenes.push({
      id: attribute(attributes, 'data-aun-scene'),
      title: attribute(attributes, 'data-aun-title'),
      surface: attribute(attributes, 'data-aun-surface'),
      intro: attribute(attributes, 'data-aun-intro'),
      html: html.slice(bodyStart, bodyEnd).trim(),
    });
    open.lastIndex = bodyEnd;
  }
  return scenes;
}

function findSectionClose(html, from) {
  const tag = /<(\/?)section\b/gi;
  tag.lastIndex = from;
  let depth = 1;
  let match;
  while ((match = tag.exec(html)) !== null) {
    depth += match[1] ? -1 : 1;
    if (depth === 0) return match.index;
  }
  throw new Error('A New UI scene is missing its closing </section>.');
}

/**
 * Replaces every include with its partial, marking the nav entry the scene named as current.
 * `aria-current` is what carries selection: the stylesheet selects on it, so a reader of the
 * markup and a screen reader agree about which entry is active.
 */
function applyIncludes(html, partials) {
  return html.replace(
    /<div\s+data-aun-include="([^"]+)"(?:\s+data-aun-current="([^"]*)")?\s*><\/div>/g,
    (whole, name, current) => {
      const partial = partials.get(name);
      if (partial === undefined) return whole;
      return current ? markCurrent(partial, current) : partial;
    },
  );
}

function markCurrent(partial, current) {
  return partial.replace(
    new RegExp(`(<a\\b[^>]*\\bdata-aun-nav="${escapeRegExp(current)}")`, 'g'),
    '$1 aria-current="page"',
  );
}

function attribute(attributes, name) {
  const match = attributes.match(new RegExp(`${escapeRegExp(name)}="([^"]*)"`));
  return match ? match[1] : '';
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function stripMarkup(value) {
  return value.replace(/&middot;/g, '·').replace(/<[^>]+>/g, '').trim();
}

function escapeText(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}
