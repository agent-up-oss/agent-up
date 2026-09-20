const TYPESCRIPT = {
  id: 'typescript',
  extensions: ['.ts', '.tsx', '.js', '.jsx', '.mjs', '.cjs'],
  keywords: [
    'as', 'async', 'await', 'break', 'case', 'catch', 'class', 'const', 'continue',
    'debugger', 'default', 'delete', 'do', 'else', 'export', 'extends', 'false',
    'finally', 'for', 'from', 'function', 'if', 'import', 'in', 'instanceof', 'let',
    'new', 'null', 'of', 'return', 'static', 'super', 'switch', 'this', 'throw',
    'true', 'try', 'typeof', 'undefined', 'var', 'void', 'while', 'with', 'yield',
    'type', 'interface', 'enum', 'implements', 'private', 'protected', 'public',
    'readonly', 'infer', 'never', 'unknown', 'satisfies',
  ],
  types: ['string', 'number', 'boolean', 'any', 'bigint', 'symbol', 'object'],
  lineComment: '//',
  blockComment: ['/*', '*/'],
  strings: ['"', "'", '`'],
};

const CSHARP = {
  id: 'csharp',
  extensions: ['.cs'],
  keywords: [
    'abstract', 'as', 'async', 'await', 'base', 'break', 'case', 'catch', 'checked',
    'class', 'const', 'continue', 'default', 'delegate', 'do', 'else', 'enum',
    'event', 'explicit', 'extern', 'false', 'finally', 'fixed', 'for', 'foreach',
    'goto', 'if', 'implicit', 'in', 'interface', 'internal', 'is', 'lock',
    'namespace', 'new', 'null', 'operator', 'out', 'override', 'params', 'private',
    'protected', 'public', 'readonly', 'ref', 'return', 'sealed', 'sizeof',
    'stackalloc', 'static', 'struct', 'switch', 'this', 'throw', 'true', 'try',
    'typeof', 'unchecked', 'unsafe', 'using', 'virtual', 'void', 'volatile',
    'while', 'record', 'required', 'init', 'with', 'when', 'where', 'yield',
    'get', 'set', 'file', 'partial',
  ],
  types: [
    'bool', 'byte', 'char', 'decimal', 'double', 'float', 'int', 'long', 'object',
    'sbyte', 'short', 'string', 'uint', 'ulong', 'ushort', 'nint', 'nuint', 'var',
    'dynamic', 'Task', 'Action', 'Func',
  ],
  lineComment: '//',
  blockComment: ['/*', '*/'],
  strings: ['"', "'"],
};

const PYTHON = {
  id: 'python',
  extensions: ['.py'],
  keywords: [
    'and', 'as', 'assert', 'async', 'await', 'break', 'class', 'continue', 'def',
    'del', 'elif', 'else', 'except', 'False', 'finally', 'for', 'from', 'global',
    'if', 'import', 'in', 'is', 'lambda', 'None', 'nonlocal', 'not', 'or', 'pass',
    'raise', 'return', 'True', 'try', 'while', 'with', 'yield',
  ],
  types: ['int', 'str', 'float', 'bool', 'list', 'dict', 'set', 'tuple', 'bytes'],
  lineComment: '#',
  blockComment: ['"""', '"""'],
  strings: ['"', "'"],
};

const CSS = {
  id: 'css',
  extensions: ['.css'],
  keywords: ['important', 'from', 'to'],
  types: [],
  lineComment: '',
  blockComment: ['/*', '*/'],
  strings: ['"', "'"],
};

const JSON_LANG = {
  id: 'json',
  extensions: ['.json'],
  keywords: ['true', 'false', 'null'],
  types: [],
  lineComment: '',
  blockComment: null,
  strings: ['"'],
};

const HTML = {
  id: 'html',
  extensions: ['.html', '.htm', '.xml', '.axaml', '.xaml', '.svg'],
  keywords: [
    'html', 'head', 'body', 'div', 'span', 'button', 'input', 'section', 'article',
    'header', 'nav', 'main', 'true', 'false', 'null',
  ],
  types: [],
  lineComment: '',
  blockComment: ['<!--', '-->'],
  strings: ['"', "'"],
};

const MARKDOWN = {
  id: 'markdown',
  extensions: ['.md', '.mdx'],
  keywords: [],
  types: [],
  lineComment: '',
  blockComment: null,
  strings: ['`'],
};

const SHELL = {
  id: 'shell',
  extensions: ['.sh', '.bash', '.zsh'],
  keywords: [
    'if', 'then', 'else', 'elif', 'fi', 'for', 'in', 'do', 'done', 'while', 'case',
    'esac', 'function', 'return', 'export', 'local', 'readonly', 'true', 'false',
  ],
  types: [],
  lineComment: '#',
  blockComment: null,
  strings: ['"', "'"],
};

const YAML = {
  id: 'yaml',
  extensions: ['.yml', '.yaml'],
  keywords: ['true', 'false', 'null', 'yes', 'no', 'on', 'off'],
  types: [],
  lineComment: '#',
  blockComment: null,
  strings: ['"', "'"],
};

const SQL = {
  id: 'sql',
  extensions: ['.sql'],
  keywords: [
    'select', 'from', 'where', 'insert', 'into', 'update', 'delete', 'create',
    'table', 'index', 'and', 'or', 'not', 'null', 'join', 'left', 'right', 'inner',
    'on', 'as', 'order', 'by', 'group', 'limit', 'values', 'set',
  ],
  types: ['int', 'text', 'boolean', 'timestamp'],
  lineComment: '--',
  blockComment: ['/*', '*/'],
  strings: ["'", '"'],
};

const RUST = {
  id: 'rust',
  extensions: ['.rs'],
  keywords: [
    'as', 'async', 'await', 'break', 'const', 'continue', 'crate', 'dyn', 'else',
    'enum', 'extern', 'false', 'fn', 'for', 'if', 'impl', 'in', 'let', 'loop',
    'match', 'mod', 'move', 'mut', 'pub', 'ref', 'return', 'self', 'Self', 'static',
    'struct', 'super', 'trait', 'true', 'type', 'unsafe', 'use', 'where', 'while',
  ],
  types: ['i32', 'i64', 'u32', 'u64', 'f32', 'f64', 'bool', 'str', 'String', 'Vec'],
  lineComment: '//',
  blockComment: ['/*', '*/'],
  strings: ['"'],
};

const GO = {
  id: 'go',
  extensions: ['.go'],
  keywords: [
    'break', 'case', 'chan', 'const', 'continue', 'default', 'defer', 'else',
    'fallthrough', 'for', 'func', 'go', 'goto', 'if', 'import', 'interface', 'map',
    'package', 'range', 'return', 'select', 'struct', 'switch', 'type', 'var',
    'true', 'false', 'nil',
  ],
  types: ['string', 'int', 'int64', 'bool', 'byte', 'error', 'float64'],
  lineComment: '//',
  blockComment: ['/*', '*/'],
  strings: ['"', "'", '`'],
};

const JAVA = {
  id: 'java',
  extensions: ['.java', '.kt'],
  keywords: [
    'abstract', 'assert', 'break', 'case', 'catch', 'class', 'const', 'continue',
    'default', 'do', 'else', 'enum', 'extends', 'final', 'finally', 'for', 'goto',
    'if', 'implements', 'import', 'instanceof', 'interface', 'native', 'new',
    'package', 'private', 'protected', 'public', 'return', 'static', 'strictfp',
    'super', 'switch', 'synchronized', 'this', 'throw', 'throws', 'transient',
    'try', 'void', 'volatile', 'while', 'true', 'false', 'null',
  ],
  types: ['boolean', 'byte', 'char', 'double', 'float', 'int', 'long', 'short', 'String', 'var'],
  lineComment: '//',
  blockComment: ['/*', '*/'],
  strings: ['"', "'"],
};

const RUBY = {
  id: 'ruby',
  extensions: ['.rb'],
  keywords: [
    'alias', 'and', 'begin', 'break', 'case', 'class', 'def', 'do', 'else', 'elsif',
    'end', 'ensure', 'false', 'for', 'if', 'in', 'module', 'next', 'nil', 'not',
    'or', 'redo', 'rescue', 'retry', 'return', 'self', 'super', 'then', 'true',
    'undef', 'unless', 'until', 'when', 'while', 'yield',
  ],
  types: [],
  lineComment: '#',
  blockComment: ['=begin', '=end'],
  strings: ['"', "'"],
};

export const grammars = {
  languages: [
    TYPESCRIPT, CSHARP, PYTHON, CSS, JSON_LANG, HTML, MARKDOWN, SHELL, YAML, SQL,
    RUST, GO, JAVA, RUBY,
  ],
};

const byId = new Map(grammars.languages.map(language => [language.id, language]));
const byExtension = new Map();
for (const language of grammars.languages) {
  for (const extension of language.extensions) byExtension.set(extension, language.id);
}

const OPERATORS = new Set('+-*/%=<>!&|^~?:'.split(''));
const PUNCTUATION = new Set('(){}[];,.'.split(''));

export function detectLanguage(path) {
  if (!path) return 'plaintext';
  const slash = Math.max(path.lastIndexOf('/'), path.lastIndexOf('\\'));
  const name = slash >= 0 ? path.slice(slash + 1) : path;
  const dot = name.lastIndexOf('.');
  if (dot <= 0) {
    if (name === 'Dockerfile' || name === 'Makefile') return 'shell';
    return 'plaintext';
  }
  return byExtension.get(name.slice(dot).toLowerCase()) ?? 'plaintext';
}

export function tokenizeLine(text, language) {
  if (!text) return [];
  const grammar = byId.get(language);
  if (!grammar) return [{ kind: 'plain', text }];
  const keywords = new Set(grammar.keywords);
  const types = new Set(grammar.types);
  const tokens = [];
  let index = 0;

  const push = (kind, value) => {
    if (!value) return;
    const last = tokens[tokens.length - 1];
    if (last && last.kind === kind) last.text += value;
    else tokens.push({ kind, text: value });
  };

  while (index < text.length) {
    const rest = text.slice(index);
    const block = grammar.blockComment;
    if (grammar.lineComment && rest.startsWith(grammar.lineComment)) {
      push('comment', text.slice(index));
      break;
    }
    if (block && rest.startsWith(block[0])) {
      const end = text.indexOf(block[1], index + block[0].length);
      const close = end < 0 ? text.length : end + block[1].length;
      push('comment', text.slice(index, close));
      index = close;
      continue;
    }

    const quote = grammar.strings?.find(item => rest.startsWith(item));
    if (quote) {
      let cursor = index + quote.length;
      while (cursor < text.length) {
        if (text[cursor] === '\\' && cursor + 1 < text.length) {
          cursor += 2;
          continue;
        }
        if (text.startsWith(quote, cursor)) {
          cursor += quote.length;
          break;
        }
        cursor += 1;
      }
      push('string', text.slice(index, cursor));
      index = cursor;
      continue;
    }

    const current = text[index];
    if (isDigit(current) || (current === '.' && isDigit(text[index + 1]))) {
      let cursor = index + 1;
      if (current === '0' && (text[cursor] === 'x' || text[cursor] === 'X')) cursor += 1;
      while (cursor < text.length && /[0-9a-fA-Fn_.]/.test(text[cursor])) cursor += 1;
      push('number', text.slice(index, cursor));
      index = cursor;
      continue;
    }

    if (isIdentStart(current)) {
      let cursor = index + 1;
      while (cursor < text.length && isIdentPart(text[cursor])) cursor += 1;
      const word = text.slice(index, cursor);
      let kind = 'plain';
      if (keywords.has(word)) kind = 'keyword';
      else if (types.has(word)) kind = 'type';
      else {
        let look = cursor;
        while (look < text.length && text[look] === ' ') look += 1;
        if (text[look] === '(') kind = 'function';
        else if (index > 0 && text[index - 1] === '.') kind = 'property';
      }
      push(kind, word);
      index = cursor;
      continue;
    }

    if (OPERATORS.has(current)) {
      push('operator', current);
      index += 1;
      continue;
    }
    if (PUNCTUATION.has(current)) {
      push('punctuation', current);
      index += 1;
      continue;
    }

    push('plain', current);
    index += 1;
  }

  return tokens;
}

function isDigit(character) {
  return character >= '0' && character <= '9';
}

function isIdentStart(character) {
  return character === '_' || character === '$'
    || (character >= 'A' && character <= 'Z')
    || (character >= 'a' && character <= 'z');
}

function isIdentPart(character) {
  return isIdentStart(character) || isDigit(character);
}
