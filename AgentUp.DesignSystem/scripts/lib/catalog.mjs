export function parseCatalog(html) {
  const surfaces = [];
  const sectionPattern = /<section\s+([^>]*)>([\s\S]*?)<\/section>/g;
  let sectionMatch;
  while ((sectionMatch = sectionPattern.exec(html))) {
    const attrs = sectionMatch[1];
    const body = sectionMatch[2];
    const components = [];
    const articlePattern = /<article\s+([^>]*)>([\s\S]*?)<\/article>/g;
    let articleMatch;
    while ((articleMatch = articlePattern.exec(body))) {
      const articleAttrs = articleMatch[1];
      const articleHtml = articleMatch[2].trim();
      const classes = [...articleHtml.matchAll(/class="([^"]+)"/g)]
        .flatMap(match => match[1].split(/\s+/))
        .filter(name => name.startsWith('au-'));
      const uniqueClasses = [...new Set(classes)];
      const skip = new Set(['au-cluster', 'au-stack', 'au-grid', 'au-theme']);
      const rootClass = attr(articleAttrs, 'class')
        || uniqueClasses.find(name => !name.includes('--') && !skip.has(name))
        || uniqueClasses[0]
        || '';
      components.push({
        id: attr(articleAttrs, 'component'),
        title: attr(articleAttrs, 'title'),
        note: attr(articleAttrs, 'note'),
        avalonia: attr(articleAttrs, 'avalonia') || 'Border',
        desktopClass: attr(articleAttrs, 'desktop-class'),
        desktopHost: attr(articleAttrs, 'desktop-host'),
        states: attr(articleAttrs, 'states'),
        parts: attr(articleAttrs, 'parts'),
        propsExamples: attr(articleAttrs, 'props-examples'),
        modifierClasses: attr(articleAttrs, 'modifier-classes'),
        stateExamples: attr(articleAttrs, 'state-examples'),
        rootClass,
        classes: uniqueClasses,
        html: articleHtml,
      });
    }
    surfaces.push({
      id: attr(attrs, 'surface'),
      title: attr(attrs, 'title'),
      intro: attr(attrs, 'intro'),
      components,
    });
  }
  return { surfaces };
}

export function catalogIndex(catalog) {
  const types = new Map();
  const aliases = new Map();
  const hosts = new Map();
  for (const surface of catalog.surfaces) {
    for (const component of surface.components) {
      if (component.rootClass) types.set(component.rootClass, component.avalonia);
      for (const className of component.classes.filter(name => name === component.rootClass || name.startsWith(`${component.rootClass}--`))) {
        types.set(className, component.avalonia);
      }
      if (component.rootClass && component.desktopClass) aliases.set(component.rootClass, [component.desktopClass]);
      if (component.rootClass && component.desktopHost) hosts.set(component.rootClass, component.desktopHost);
      for (const [className, desktopClass] of parseModifierClasses(component.modifierClasses)) {
        aliases.set(className, [desktopClass]);
      }
    }
  }
  return {
    typeFor(className) {
      return types.get(className);
    },
    aliases(className) {
      return aliases.get(className) ?? [];
    },
    host(className) {
      return hosts.get(className);
    },
  };
}

function attr(raw, name) {
  const match = raw.match(new RegExp(`data-au-${name}="([^"]*)"`));
  return match ? match[1] : '';
}

/**
 * The Desktop class name a modifier of this component maps to.
 *
 *   data-au-modifier-classes="au-git-row--selected=gitNodeRowSelected"
 *
 * Desktop binds these with `Classes.gitNodeRowSelected="{Binding IsOpen}"` while it still
 * carries its own class vocabulary, so the generated styles have to name them. Naming a
 * modifier used to require a second catalog component whose only job was to hold the
 * attribute, which is how a state ended up with a duplicate copy of the markup.
 */
function parseModifierClasses(raw) {
  const pairs = [];
  for (const entry of (raw ?? '').split(/\s+/).filter(Boolean)) {
    const split = entry.indexOf('=');
    if (split < 1) throw new Error(`Modifier class '${entry}' is not '<class>=<desktopClass>'.`);
    const className = entry.slice(0, split);
    const desktopClass = entry.slice(split + 1);
    if (!desktopClass) throw new Error(`Modifier class '${className}' has no Desktop class name.`);
    if (!className.includes('--')) throw new Error(`'${className}' is not a modifier class.`);
    pairs.push([className, desktopClass]);
  }
  return pairs;
}
