export function stripComments(css) {
  return css.replace(/\/\*[\s\S]*?\*\//g, '');
}

export function removeAtRules(css) {
  let result = '';
  for (let i = 0; i < css.length; ) {
    if (css[i] === '@') {
      const start = css.indexOf('{', i);
      if (start < 0) break;
      let depth = 1;
      let j = start + 1;
      while (j < css.length && depth) {
        if (css[j] === '{') depth += 1;
        else if (css[j] === '}') depth -= 1;
        j += 1;
      }
      i = j;
      continue;
    }
    result += css[i];
    i += 1;
  }
  return result;
}

export function parseCustomProperties(css) {
  return Object.fromEntries(
    [...stripComments(css).matchAll(/--au-([a-z0-9-]+):\s*([^;]+);/g)].map(([, name, value]) => [name, value.trim()]),
  );
}

export function parseRules(css) {
  const text = removeAtRules(stripComments(css));
  const rules = [];
  const pattern = /([^{}]+)\{([^{}]+)\}/g;
  let match;
  while ((match = pattern.exec(text))) {
    const selectors = match[1].split(',').map(value => value.trim()).filter(Boolean);
    const declarations = [];
    for (const line of match[2].split(';')) {
      const idx = line.indexOf(':');
      if (idx < 0) continue;
      const property = line.slice(0, idx).trim();
      const value = line.slice(idx + 1).trim();
      if (!property || !value) continue;
      if (property.startsWith('--')) continue;
      declarations.push([property, value]);
    }
    if (!selectors.length || !declarations.length) continue;
    rules.push({ selectors, declarations });
  }
  return rules;
}

export function pascal(name) {
  return name.split('-').map(part => part[0].toUpperCase() + part.slice(1)).join('');
}

export function camel(name) {
  return name.replace(/-([a-z0-9])/g, (_, character) => character.toUpperCase());
}

export function tokenKey(name) {
  return `AgentUp${pascal(name)}`;
}

export function toPx(value) {
  if (value == null) return null;
  const raw = String(value).trim();
  if (raw === '0') return 0;
  const clamp = raw.match(/^clamp\(\s*([^,]+),/i);
  if (clamp) return toPx(clamp[1]);
  const rem = raw.match(/^(-?[0-9.]+)rem$/);
  if (rem) return Number(rem[1]) * 16;
  const px = raw.match(/^(-?[0-9.]+)px$/);
  if (px) return Number(px[1]);
  const number = raw.match(/^(-?[0-9.]+)$/);
  if (number) return Number(number[1]);
  return null;
}

export function formatNumber(value) {
  if (Number.isInteger(value)) return String(value);
  return String(Math.round(value * 100) / 100);
}

export function varName(value) {
  const match = String(value).trim().match(/^var\(--au-([a-z0-9-]+)\)$/);
  return match ? match[1] : null;
}

export function resolveVars(value, tokens) {
  return String(value).replace(/var\(--au-([a-z0-9-]+)\)/g, (_, name) => tokens[name] ?? _);
}

export function nativeLiteral(value, tokens) {
  const token = varName(value);
  const resolved = token ? tokens[token] : String(value).trim();
  if (token?.startsWith('color-') || /^#|^rgba?\(/i.test(resolved)) return JSON.stringify(resolved);
  const px = toPx(resolved);
  if (px != null) return formatNumber(px);
  if (/^[0-9.]+$/.test(resolved)) return resolved;
  return JSON.stringify(resolved);
}

export function parseSelector(selector) {
  const raw = selector.trim();
  if (!raw || raw === '*' || raw.includes('::') || raw.startsWith(':root') || raw.includes(' ')) return null;
  const selected = /\[aria-selected=['"]true['"]\]/.test(raw);
  const withoutAttr = raw.replace(/\[aria-selected=['"]true['"]\]/, '');
  const pseudoMatch = withoutAttr.match(/:(hover|focus|focus-visible|disabled|checked)$/);
  const pseudo = pseudoMatch ? pseudoMatch[1].replace('focus-visible', 'focus') : null;
  const classPart = withoutAttr.replace(/:(hover|focus|focus-visible|disabled|checked)$/, '');
  const classes = [...classPart.matchAll(/\.([a-z0-9-]+)/g)].map(match => match[1]);
  if (!classes.length) return null;
  const modifierClass = classes.find(name => name.includes('--')) ?? null;
  const baseClass = classes.find(name => !name.includes('--'))
    ?? (modifierClass ? modifierClass.slice(0, modifierClass.indexOf('--')) : classes[0]);
  return { baseClass, modifierClass, classes, pseudo, selected };
}

export function inferAvaloniaType(className) {
  if (className === 'au-theme') return 'Window';
  if (/^au-button(?:--|$)|au-chip(?:--|$)|au-tab(?:--|$)|au-chrome-button|au-title-tool|au-workspace-delete|au-workspace-add|au-lifecycle-button|au-browser-button|au-page-jump|au-db-run|au-tutorial-button|au-app-tab|au-subtab/.test(className)) {
    return 'Button';
  }
  if (/(au-input|au-address-bar|au-code-editor|au-console|au-code$)/.test(className)) return 'TextBox';
  if (/(au-eyebrow|au-display|au-title|au-page-title|au-heading|au-lede|au-muted|au-mono|au-accent|au-field-label|au-chrome-icon|au-workspace-name|au-workspace-branch)/.test(className)) return 'TextBlock';
  return 'Border';
}

export const layoutOnly = new Set([
  'au-container', 'au-stack', 'au-cluster', 'au-grid', 'au-section', 'au-reading',
]);
