/** Eval contract used by Validation replay against the visible application document. */
export type RemoteBrowserHandle = {
  eval(script: string): Promise<string | null>;
};

export function stringifyEvalResult(result: unknown): string {
  if (result == null) return 'null';
  if (typeof result === 'string') return result;
  if (typeof result === 'boolean' || typeof result === 'number') return String(result);
  try {
    return JSON.stringify(result);
  } catch {
    return String(result);
  }
}

/** JSON.stringify leaves U+2028/U+2029 intact; those must be escaped before the literal is used as code. */
export function javascriptLiteral(value: unknown): string {
  return JSON.stringify(value)
    .replace(/\u2028/g, '\\u2028')
    .replace(/\u2029/g, '\\u2029');
}
