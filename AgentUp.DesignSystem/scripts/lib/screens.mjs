export const desktopSize = { width: 1440, height: 900 };
export const mobileSize = { width: 390, height: 844 };

const slotPattern = () => /<([a-z][a-z0-9]*)([^>]*?)\sdata-au-slot="([^"]+)"([^>]*)>\s*<\/\1>/gi;
const regionPattern = () => /<div\s+data-au-region="([^"]+)"\s+data-au-use="([^"]+)"\s*><\/div>/g;
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

export function wrapSceneDocument(scene, css) {
  const size = scene.surface === 'mobile' ? mobileSize : desktopSize;
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=${size.width}, height=${size.height}, initial-scale=1" />
  <title>${escapeHtml(scene.title)}</title>
  <style>${css}</style>
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
  const html = resolveUses(slotted, components);
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
    html,
  };
}

function applySlots(layout, regions) {
  const slots = new Map();
  for (const region of regions) {
    const marker = `<div data-au-use="${region.use}"></div>`;
    slots.set(region.name, `${slots.get(region.name) ?? ''}${marker}`);
  }
  return layout.replace(slotPattern(), (full, tag, before, name, after) => {
    const inner = slots.get(name);
    if (!inner) return '';
    const attrs = `${before}${after}`.trim();
    return attrs ? `<${tag} ${attrs}>${inner}</${tag}>` : `<${tag}>${inner}</${tag}>`;
  });
}

export function isLayoutShell(html) {
  return shellPattern.test(html.trim());
}

function resolveUses(html, components) {
  let current = html;
  for (let depth = 0; depth < 64; depth += 1) {
    const uses = findUses(current);
    if (!uses.length) return current;
    const inner = uses.find(use => !uses.some(other => other !== use && other.start > use.start && other.end < use.end));
    const component = mustGet(inner.id, components);
    const replacement = inner.inner.trim() === ''
      ? component.html
      : applyShell(inner.id, inner.inner, component);
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
    regions.push({ name: match[1], use: match[2] });
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
