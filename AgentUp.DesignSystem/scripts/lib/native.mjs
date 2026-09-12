import { camel, layoutOnly, nativeLiteral, parseSelector, toPx, varName } from './css.mjs';

const boxKeys = new Set([
  'backgroundColor', 'borderWidth', 'borderColor', 'borderRadius',
  'borderTopWidth', 'borderTopColor', 'borderRightWidth', 'borderRightColor',
  'borderBottomWidth', 'borderBottomColor', 'borderLeftWidth', 'borderLeftColor',
  'width', 'height', 'minHeight', 'minWidth', 'paddingHorizontal', 'paddingVertical',
  'opacity',
]);
const textKeys = new Set(['color', 'fontSize', 'fontWeight', 'opacity', 'fontFamily', 'textTransform']);

export function emitNative(tokens, rules) {
  const groups = {
    colors: Object.entries(tokens).filter(([name]) => name.startsWith('color-')),
    spacing: Object.entries(tokens).filter(([name]) => name.startsWith('space-')),
    radii: Object.entries(tokens).filter(([name]) => name.startsWith('radius-')),
    typography: Object.entries(tokens).filter(([name]) => name.startsWith('font-size-') || name.startsWith('weight-') || name.startsWith('line-')),
    controls: Object.entries(tokens).filter(([name]) => name.startsWith('control-height')),
    fonts: Object.entries(tokens).filter(([name]) => name === 'font-sans' || name === 'font-mono'),
  };
  const components = {};
  for (const rule of rules) {
    for (const selector of rule.selectors) {
      const parsed = parseSelector(selector);
      if (!parsed || parsed.pseudo || parsed.selected || layoutOnly.has(parsed.baseClass)) continue;
      const name = camel((parsed.modifierClass ?? parsed.baseClass).replace(/^au-/, '').replaceAll('--', '-'));
      const style = rnStyle(rule.declarations, tokens);
      if (!Object.keys(style).length) continue;
      components[name] = { ...components[name], ...style };
    }
  }
  const componentBlock = Object.entries(components).map(([name, style]) =>
    `    ${name}: Object.freeze({\n${Object.entries(style).map(([key, value]) => `      ${key}: ${value},`).join('\n')}\n    }),`
  ).join('\n');
  const helpers = nativeHelpers();
  const themeExpression = `Object.freeze({\n${Object.entries(groups).map(([group, values]) =>
    `  ${group}: Object.freeze({\n${values.map(([name, value]) => `    ${nativeKey(group, name)}: ${nativeLiteral(value, tokens)},`).join('\n')}\n  }),`
  ).join('\n')}\n  components: Object.freeze({\n${componentBlock}\n  }),\n})`;
  const js = `// Generated from src/agent-up.css by scripts/build.mjs. Do not edit.\nexport const agentUpTheme = ${themeExpression};\n${helpers.esm}\n`;
  const cjs = `// Generated from src/agent-up.css by scripts/build.mjs. Do not edit.\nconst agentUpTheme = ${themeExpression};\n${helpers.cjs}\nmodule.exports = { agentUpTheme, auBox, auText };\n`;
  const dts = `// Generated from src/agent-up.css. Do not edit.\nexport declare const agentUpTheme: {\n  readonly colors: Readonly<Record<string, string>>;\n  readonly spacing: Readonly<Record<string, number>>;\n  readonly radii: Readonly<Record<string, number>>;\n  readonly typography: {\n    readonly sizeXs: number;\n    readonly sizeSm: number;\n    readonly sizeMd: number;\n    readonly sizeLg: number;\n    readonly sizeXl: number;\n    readonly sizeDisplay: number;\n    readonly tight: number;\n    readonly normal: number;\n    readonly relaxed: number;\n    readonly regular: number;\n    readonly medium: number;\n    readonly semibold: number;\n    readonly bold: number;\n    readonly black: number;\n  };\n  readonly controls: Readonly<Record<string, number>>;\n  readonly fonts: Readonly<Record<string, string>>;\n  readonly components: Readonly<Record<string, Readonly<Record<string, string | number>>>>;\n};\nexport declare function auBox(...names: Array<string | false | null | undefined>): {\n  backgroundColor?: string;\n  borderWidth?: number;\n  borderColor?: string;\n  borderRadius?: number;\n  borderTopWidth?: number;\n  borderTopColor?: string;\n  borderRightWidth?: number;\n  borderRightColor?: string;\n  borderBottomWidth?: number;\n  borderBottomColor?: string;\n  borderLeftWidth?: number;\n  borderLeftColor?: string;\n  width?: number;\n  height?: number;\n  minHeight?: number;\n  minWidth?: number;\n  paddingHorizontal?: number;\n  paddingVertical?: number;\n  opacity?: number;\n};\nexport declare function auText(...names: Array<string | false | null | undefined>): {\n  color?: string;\n  fontSize?: number;\n  fontWeight?: '400' | '500' | '600' | '700' | '800';\n  opacity?: number;\n  fontFamily?: string;\n  textTransform?: 'none' | 'capitalize' | 'uppercase' | 'lowercase';\n};\n`;
  return { js, cjs, dts };
}

function nativeHelpers() {
  const body = `const auBoxKeys = new Set(${JSON.stringify([...boxKeys])});
const auTextKeys = new Set(${JSON.stringify([...textKeys])});
function auPick(names, keys) {
  const out = {};
  for (const name of names) {
    if (!name) continue;
    const style = agentUpTheme.components[name];
    if (!style) throw new Error("Unknown Agent-Up component '" + name + "'.");
    for (const [key, value] of Object.entries(style)) {
      if (keys.has(key)) out[key] = value;
    }
  }
  return out;
}`;
  return {
    esm: `${body}
export function auBox(...names) { return auPick(names, auBoxKeys); }
export function auText(...names) { return auPick(names, auTextKeys); }
`,
    cjs: `${body}
function auBox(...names) { return auPick(names, auBoxKeys); }
function auText(...names) { return auPick(names, auTextKeys); }
`,
  };
}

function nativeKey(group, name) {
  if (group === 'colors') return camel(name.replace(/^color-/, ''));
  if (group === 'spacing') return name.replace(/^space-/, '');
  if (group === 'radii') return camel(name.replace(/^radius-/, ''));
  if (group === 'controls') return camel(name.replace(/^control-/, ''));
  if (group === 'fonts') return camel(name.replace(/^font-/, ''));
  if (name.startsWith('font-size-')) return camel(`size-${name.replace(/^font-size-/, '')}`);
  if (name.startsWith('weight-')) return camel(name.replace(/^weight-/, ''));
  if (name.startsWith('line-')) return camel(name.replace(/^line-/, ''));
  return camel(name);
}

function rnStyle(declarations, tokens) {
  const style = {};
  let colorLiteral = null;
  let backgroundIsCurrentColor = false;
  for (const [property, value] of declarations) {
    if (/color-mix\(|min\(|max\(|calc\(/i.test(value) && !value.startsWith('clamp(')) continue;
    switch (property) {
      case 'color':
        colorLiteral = nativeLiteral(value, tokens);
        style.color = colorLiteral;
        break;
      case 'background':
      case 'background-color':
        if (value === 'currentColor') backgroundIsCurrentColor = true;
        else style.backgroundColor = nativeLiteral(value, tokens);
        break;
      case 'border-radius':
        style.borderRadius = radiusLiteral(value, tokens);
        break;
      case 'min-height':
        style.minHeight = nativeLiteral(value, tokens);
        break;
      case 'min-width':
        style.minWidth = nativeLiteral(value, tokens);
        break;
      case 'width':
        if (!value.includes('%') && !value.includes('min(')) style.width = nativeLiteral(value, tokens);
        break;
      case 'height':
        if (!value.includes('%')) style.height = nativeLiteral(value, tokens);
        break;
      case 'font-size':
        style.fontSize = nativeLiteral(value, tokens);
        break;
      case 'font-family':
        style.fontFamily = nativeLiteral(value, tokens);
        break;
      case 'font-weight': {
        const resolved = String(value).includes('var(') ? nativeLiteral(value, tokens) : value;
        style.fontWeight = /^[0-9]+$/.test(String(resolved)) ? `'${resolved}'` : nativeLiteral(value, tokens);
        break;
      }
      case 'text-transform':
        style.textTransform = `'${value}'`;
        break;
      case 'opacity':
        style.opacity = value;
        break;
      case 'padding': {
        const parts = padding(value, tokens);
        if (!parts) break;
        style.paddingHorizontal = parts.horizontal;
        style.paddingVertical = parts.vertical;
        break;
      }
      case 'border':
      case 'border-top':
      case 'border-right':
      case 'border-bottom':
      case 'border-left': {
        const parsed = parseBorder(value, tokens);
        if (property === 'border') {
          style.borderWidth = parsed.width;
          if (parsed.color) style.borderColor = parsed.color;
        } else {
          const edge = property.slice('border-'.length);
          const suffix = edge[0].toUpperCase() + edge.slice(1);
          style[`border${suffix}Width`] = parsed.width;
          if (parsed.color) style[`border${suffix}Color`] = parsed.color;
        }
        break;
      }
      case 'border-color':
        style.borderColor = nativeLiteral(value, tokens);
        break;
      default:
        break;
    }
  }
  if (backgroundIsCurrentColor && colorLiteral) style.backgroundColor = colorLiteral;
  return style;
}

function radiusLiteral(value, tokens) {
  const token = varName(value);
  if (token === 'radius-pill' || String(value).trim() === '50%') return '999';
  return nativeLiteral(value, tokens);
}

function parseBorder(value, tokens) {
  if (value === '0' || value === 'none') return { width: 0, color: nativeLiteral('var(--au-color-transparent)', tokens) };
  const parts = value.split(/\s+/);
  let width = 1;
  let color = null;
  for (const part of parts) {
    if (part === 'solid' || part === 'none' || part === 'dashed' || part === 'dotted') continue;
    const token = varName(part);
    if (token === 'stroke' || token === 'stroke-strong' || toPx(part) != null || (token && toPx(tokens[token]) != null && !token.startsWith('color-'))) {
      const px = token ? toPx(tokens[token]) : toPx(part);
      if (px != null) width = px;
    } else {
      color = nativeLiteral(part, tokens);
    }
  }
  return { width, color };
}

function padding(value, tokens) {
  const parts = value.trim().split(/\s+/).map(part => {
    const px = toPx(part.includes('var(') ? nativeLiteral(part, tokens).replaceAll('"', '') : part);
    if (part.includes('var(')) {
      const token = part.match(/--au-([a-z0-9-]+)/);
      if (token) return nativeLiteral(part, tokens);
    }
    return px == null ? null : String(px);
  });
  if (parts.some(part => part == null)) return null;
  if (parts.length === 1) return { vertical: parts[0], horizontal: parts[0] };
  if (parts.length === 2) return { vertical: parts[0], horizontal: parts[1] };
  if (parts.length === 4) return { vertical: parts[0], horizontal: parts[1] };
  return null;
}
