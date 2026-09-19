export type SyntaxKind =
  | 'plain'
  | 'keyword'
  | 'type'
  | 'string'
  | 'comment'
  | 'number'
  | 'function'
  | 'property'
  | 'punctuation'
  | 'operator';

export type SyntaxToken = {
  kind: SyntaxKind;
  text: string;
};

export type SyntaxLanguage = {
  id: string;
  extensions: string[];
  keywords: string[];
  types: string[];
  lineComment: string;
  blockComment: [string, string] | null;
  strings: string[];
};

export declare const grammars: { languages: SyntaxLanguage[] };
export declare function detectLanguage(path: string): string;
export declare function tokenizeLine(text: string, language: string): SyntaxToken[];
