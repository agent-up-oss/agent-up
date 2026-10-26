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
