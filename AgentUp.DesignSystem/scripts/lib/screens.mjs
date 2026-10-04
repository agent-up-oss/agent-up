import { buildSequence } from './sequences.mjs';
import { applyProps } from './slots.mjs';
import { applyStates, declaredModifiers, parseStateDeclarations, parseStateRequest } from './states.mjs';

export const desktopSize = { width: 1440, height: 900 };
export const mobileSize = { width: 390, height: 844 };

const slotPattern = () => /<([a-z][a-z0-9]*)([^>]*?)\sdata-au-slot="([^"]+)"([^>]*)>\s*<\/\1>/gi;
const regionPattern = () => /<div\s+data-au-region="([^"]+)"\s+data-au-use="([^"]+)"((?:\s+data-au-[a-z-]+="[^"]*")*)\s*><\/div>/g;
const sectionPattern = () => /<section\s+([^>]*)>([\s\S]*?)<\/section>/g;
const useOpenPattern = () => /<([a-z][a-z0-9]*)([^>]*?)\sdata-au-use="([^"]+)"([^>]*)>/gi;
const shellPattern = /^<([a-z][a-z0-9]*)([^>]*)>\s*<\/\1>$/i;

export function parseScreens(html) {
  const layouts = new Map();
  const screens = [];
  const scenes = [];
  const section = sectionPattern();
  let match;
  while ((match = section.exec(html))) {
    const attrs = match[1];
    const body = match[2].trim();
    const layout = attr(attrs, 'layout');
    const screen = attr(attrs, 'screen');
    const scene = attr(attrs, 'scene');
    if (layout && !screen && !scene) {
      layouts.set(layout, body);
      continue;
    }
    if (screen) {
      screens.push({
        id: screen,
        title: attr(attrs, 'title'),
        intro: attr(attrs, 'intro'),
        desktopId: attr(attrs, 'desktop'),
        mobileId: attr(attrs, 'mobile'),
      });
      continue;
    }
    if (scene) {
      scenes.push({
        id: scene,
        platform: attr(attrs, 'platform'),
        view: attr(attrs, 'view'),
        title: attr(attrs, 'title'),
        layout,
        hero: attr(attrs, 'hero') === 'true',
        livePath: attr(attrs, 'live'),
        sequence: attr(attrs, 'sequence'),
        regions: parseRegions(body),
      });
    }
  }
  return { layouts, screens, scenes };
}

export function assembleScreens(catalog, source) {
  const components = componentIndex(catalog);
  const { layouts, screens, scenes } = parseScreens(source);
  if (!layouts.size) throw new Error('Assembled screens are missing layout templates.');
  if (!screens.length) throw new Error('Assembled screens are missing screen pairings.');
  if (!scenes.length) throw new Error('Assembled screens are missing scenes.');
  rejectRepeatedStatedComponents(scenes);
  return {
    screens: screens.map(screen => ({
      id: screen.id,
      title: screen.title,
      intro: screen.intro,
      desktopId: screen.desktopId,
      mobileId: screen.mobileId,
    })),
    scenes: scenes.map(scene => assembleScene(scene, layouts, components)),
  };
}

export function framedSceneHtml(scene) {
  return sizeScreen(scene.html, scene.surface);
}

/**
 * The text a scene puts on screen.
 *
 * `au-debug screenshots validate --live` asks the hosted client for the same page and checks
 * this copy appears on it, which is the only automated statement that a documented screen and
 * the real one say the same thing. The scene carried no copy before, so that check iterated an
 * empty list and passed on every screen including the ones that did not match.
 *
 * Icon markup, single glyphs and bare numbers are left out: they are chrome the live client
 * draws its own way, and a live page missing "1" says nothing about whether it matched.
 */
export function sceneCopy(scene) {
  const text = scene.html
    .replace(/<svg[\s\S]*?<\/svg>/g, ' ')
    .replace(/<[^>]+>/g, '\u0000')
    .split('\u0000')
    .map(value => decodeEntities(value).replace(/\s+/g, ' ').trim())
    .filter(isComparableCopy);
  return [...new Set(text)].sort();
}

function isComparableCopy(value) {
  if (value.length < 3) return false;
  if (!/[A-Za-z]{3}/.test(value)) return false;
  return true;
}

function decodeEntities(value) {
  return value
    .replaceAll('&lt;', '<')
    .replaceAll('&gt;', '>')
    .replaceAll('&quot;', '"')
    .replaceAll('&#39;', "'")
    .replaceAll('&amp;', '&');
}

/**
 * The stylesheet is linked rather than inlined. Twenty-two scene documents carrying the same
 * two thousand lines made the design system's committed output almost entirely one repeated
 * file, and the capture already resolves the document over file://, so a sibling href resolves
 * with it.
 */
export const sceneStylesheetHref = '../screenshots.css';

export function wrapSceneDocument(scene) {
  const size = scene.surface === 'mobile' ? mobileSize : desktopSize;
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=${size.width}, height=${size.height}, initial-scale=1" />
  <title>${escapeHtml(scene.title)}</title>
  <link rel="stylesheet" href="${sceneStylesheetHref}" />
</head>
<body class="au-theme">
${framedSceneHtml(scene)}
</body>
</html>
`;
}

function assembleScene(scene, layouts, components) {
  const layout = layouts.get(scene.layout);
  if (!layout) throw new Error(`${scene.id} references unknown layout '${scene.layout}'.`);
  const slotted = applySlots(layout, scene.regions);
  const uses = [...slotted.matchAll(/data-au-use="([^"]+)"/g)].map(match => match[1]);
  // A component whose copy the scene replaced cannot be re-derived from the catalog, so the
  // resolved fragment is recorded and the screenshot contract compares against that instead.
  const fragments = new Map();
  const html = resolveUses(slotted, components, fragments);
  const unique = [...new Set(uses)];
  const size = scene.platform === 'mobile' ? mobileSize : desktopSize;
  return {
    id: scene.id,
    surface: scene.platform,
    view: scene.view,
    title: scene.title,
    mediaFile: `${scene.id}.png`,
    htmlFile: `${scene.id}.html`,
    width: size.width,
    height: size.height,
    hero: scene.hero,
    livePath: scene.livePath,
    layout: scene.layout,
    components: unique,
    requiredClasses: unique.map(id => components.get(id).rootClass).filter(Boolean),
    stateModifiers: sceneStateModifiers(unique, components),
    sequence: buildSequence({ id: scene.id, components: unique }, scene.sequence, components),
    componentFragments: Object.fromEntries([...fragments].sort(([a], [b]) => a.localeCompare(b))),
    html,
  };
}

/**
 * The classes a scene is allowed to have moved relative to the catalog. The screenshot
 * contract compares a scene against the catalog verbatim; a parameterised component differs
 * from its catalog example in exactly these classes and the aria attributes that track them,
 * so the contract neutralises them on both sides instead of giving up and trusting the scene.
 */
function sceneStateModifiers(ids, components) {
  const modifiers = new Set();
  for (const id of ids) {
    for (const modifier of declaredModifiers(parseStateDeclarations(components.get(id).states))) {
      modifiers.add(modifier);
    }
  }
  return [...modifiers].sort();
}

function applySlots(layout, regions) {
  const slots = new Map();
  for (const region of regions) {
    const state = region.state ? ` data-au-state="${region.state}"` : '';
    const props = region.props ? ` data-au-props="${region.props}"` : '';
    const marker = `<div data-au-use="${region.use}"${state}${props}></div>`;
    slots.set(region.name, `${slots.get(region.name) ?? ''}${marker}`);
  }
  return layout.replace(slotPattern(), (full, tag, before, name, after) => {
    const inner = slots.get(name);
    if (!inner) return '';
    const attrs = `${before}${after}`.trim();
    if (!attrs) return inner;
    return `<${tag} ${attrs}>${inner}</${tag}>`;
  });
}

export function isLayoutShell(html) {
  return shellPattern.test(html.trim());
}

function resolveUses(html, components, fragments) {
  let current = html;
  for (let depth = 0; depth < 64; depth += 1) {
    const uses = findUses(current);
    if (!uses.length) return current;
    const inner = uses.find(use => !uses.some(other => other !== use && other.start > use.start && other.end < use.end));
    const component = mustGet(inner.id, components);
    const propped = inner.props ? applyProps(component, inner.props) : component.html;
    const resolved = inner.state ? applyStates({ ...component, html: propped }, inner.state) : propped;
    if (inner.props) fragments.set(inner.id, resolved);
    const replacement = inner.inner.trim() === ''
      ? resolved
      : applyShell(inner.id, inner.inner, { ...component, html: resolved });
    current = `${current.slice(0, inner.start)}${replacement}${current.slice(inner.end)}`;
  }
  throw new Error('Assembled screen uses nested too deeply.');
}

function findUses(html) {
  const uses = [];
  const open = useOpenPattern();
  let match;
  while ((match = open.exec(html))) {
    const tag = match[1];
    const closeAt = matchingClose(html, tag, match.index + match[0].length);
    if (closeAt < 0) throw new Error(`Unclosed data-au-use '${match[3]}'.`);
    const end = closeAt + `</${tag}>`.length;
    uses.push({
      id: match[3],
      state: attr(`${match[2]}${match[4]}`, 'state'),
      props: attr(`${match[2]}${match[4]}`, 'props'),
      start: match.index,
      end,
      inner: html.slice(match.index + match[0].length, closeAt),
    });
  }
  return uses;
}

function matchingClose(html, tag, from) {
  const open = new RegExp(`<${tag}\\b[^>]*>`, 'gi');
  const close = new RegExp(`</${tag}>`, 'gi');
  let depth = 1;
  let cursor = from;
  while (cursor < html.length && depth > 0) {
    open.lastIndex = cursor;
    close.lastIndex = cursor;
    const nextOpen = open.exec(html);
    const nextClose = close.exec(html);
    if (!nextClose) return -1;
    if (nextOpen && nextOpen.index < nextClose.index) {
      depth += 1;
      cursor = nextOpen.index + nextOpen[0].length;
      continue;
    }
    depth -= 1;
    if (depth === 0) return nextClose.index;
    cursor = nextClose.index + nextClose[0].length;
  }
  return -1;
}

function applyShell(id, inner, component) {
  const shell = component.html.trim().match(shellPattern);
  if (!shell) {
    throw new Error(`${id} is a catalog example, not a layout shell. Use it as an empty data-au-use.`);
  }
  const attrs = shell[2].trim();
  return attrs ? `<${shell[1]} ${attrs}>${inner}</${shell[1]}>` : `<${shell[1]}>${inner}</${shell[1]}>`;
}

function mustGet(id, components) {
  const component = components.get(id);
  if (!component) throw new Error(`Assembled screen references unknown catalog component '${id}'.`);
  return component;
}

function parseRegions(body) {
  const regions = [];
  let match;
  const region = regionPattern();
  while ((match = region.exec(body))) {
    regions.push({
      name: match[1],
      use: match[2],
      state: attr(match[3] ?? '', 'state'),
      props: attr(match[3] ?? '', 'props'),
    });
  }
  return regions;
}

function componentIndex(catalog) {
  const map = new Map();
  for (const surface of catalog.surfaces) {
    for (const component of surface.components) map.set(component.id, component);
  }
  return map;
}

function sizeScreen(html, platform) {
  const size = platform === 'mobile' ? mobileSize : desktopSize;
  return html.replace(
    /class="au-screen"/,
    `class="au-theme au-screen" style="width:${size.width}px;height:${size.height}px"`,
  );
}

function attr(raw, name) {
  const match = raw.match(new RegExp(`data-au-${name}="([^"]*)"`));
  return match ? match[1] : '';
}

function escapeHtml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}

/**
 * One scene using the same component twice with different states would need two expected
 * fragments under one id, which the contract cannot express. No scene does it; this makes the
 * day one does a build failure rather than a check that quietly compares the wrong fragment.
 */
function rejectRepeatedStatedComponents(scenes) {
  for (const scene of scenes) {
    const states = new Map();
    for (const region of scene.regions) {
      if (!region.state) continue;
      const seen = states.get(region.use);
      if (seen !== undefined && seen !== region.state) {
        throw new Error(`${scene.id} uses '${region.use}' with two different states ('${seen}' and '${region.state}').`);
      }
      states.set(region.use, region.state);
      parseStateRequest(region.state);
    }
  }
}
