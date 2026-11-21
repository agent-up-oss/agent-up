import { grammars, tokenizeLine, type SyntaxToken } from '@agent-up/design-system/syntax';

/**
 * Highlighting for the transcript, on the design system's grammars, so the Chat module, Mobile
 * Git and Desktop colour code the same way. Mirrors Desktop's SyntaxHighlightProvider: a fence
 * tag names a grammar by id, by a common alias, or by a file extension.
 */
export type { SyntaxToken };

const aliases: Record<string, string> = {
  javascript: 'typescript', js: 'typescript', ts: 'typescript', tsx: 'typescript', jsx: 'typescript',
  sh: 'shell', bash: 'shell', zsh: 'shell', console: 'shell', 'shell-session': 'shell',
  py: 'python', cs: 'csharp', 'c#': 'csharp', yml: 'yaml', kotlin: 'java',
  rb: 'ruby', rs: 'rust', golang: 'go', xml: 'html', md: 'markdown',
};

/** Longer code is drawn plain: highlighting it costs more than it shows. */
export const highlightLineLimit = 400;

export function languageForFence(tag: string | undefined): string {
  const name = tag?.trim().toLowerCase();
  if (!name) return 'plaintext';
  if (grammars.languages.some(language => language.id === name)) return name;
  if (aliases[name]) return aliases[name]!;
  return grammars.languages.find(language => language.extensions.includes(`.${name}`))?.id ?? 'plaintext';
}

export function languageForPath(path: string | undefined): string {
  const dot = path?.lastIndexOf('.') ?? -1;
  if (!path || dot < 0) return 'plaintext';
  const extension = path.slice(dot).toLowerCase();
  return grammars.languages.find(language => language.extensions.includes(extension))?.id ?? 'plaintext';
}

/** One token list per line, or null when the code is better drawn as plain text. */
export function highlightLines(code: string, language: string): SyntaxToken[][] | null {
  const lines = code.split('\n');
  if (language === 'plaintext' || lines.length > highlightLineLimit) return null;
  return lines.map(line => tokenizeLine(line, language));
}
