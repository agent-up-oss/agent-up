// Types for parity.mjs, the shared semantics of parity.json.

export type BodyKind = 'object' | 'array' | 'stream' | 'text' | 'empty';

export interface ParityAnswer {
  status: number;
  body?: BodyKind;
  keys?: string[];
}

export interface ParityEntry {
  route: string;
  params?: Record<string, string>;
  query?: Record<string, string>;
  server?: ParityAnswer;
  demo: true | { absent: string } | (ParityAnswer & { omits?: string[]; extra?: string[]; because?: string });
}

export interface ParityManifest {
  fixtures: { server: Record<string, string>; demo: Record<string, string> };
  operations: ParityEntry[];
}

export const MANIFEST: string;
export const API_REFERENCE: string;
export const DEFAULT_PARAMS: Record<string, string>;
export function operations(openapi: unknown): string[];
export function completeness(manifest: ParityManifest, openapi: unknown): string[];
export function concrete(entry: ParityEntry, fixture: Record<string, string>): { path: string; query: string };
export function bodyKind(contentType: string | null | undefined, text: string): BodyKind;
export function keysOf(kind: BodyKind, text: string): string[] | undefined;
export function expectedDemo(entry: ParityEntry): ParityAnswer | null;
export function compare(route: string, expected: ParityAnswer, observed: ParityAnswer): string[];
