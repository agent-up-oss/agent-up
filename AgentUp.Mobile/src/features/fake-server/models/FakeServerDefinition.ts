export type FakeServerDefinition = {
  id: string;
  url: string;
  displayName: string;
  connection: unknown;
  authentication: unknown;
  entitlements: unknown;
  workspaces: unknown[];
  overview?: Record<string, unknown>;
  git?: Record<string, Record<string, unknown>>;
  console?: Record<string, unknown>;
  agents?: Record<string, { session?: unknown; scripts?: Array<{ events?: Array<{ type?: string; payload?: unknown }> }> }>;
  pages?: Record<string, string>;
  capabilities?: unknown[];
  /** What a connected repository host reports, per workspace. */
  forge?: Record<string, unknown>;
};

export function cloneDefinition(definition: FakeServerDefinition): FakeServerDefinition {
  return structuredClone(definition);
}
