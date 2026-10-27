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
  validationFlows?: unknown[];
  databases?: unknown[];
  /** What a connected repository host reports, per workspace. */
  forge?: Record<string, unknown>;
  /** The tasks picked for the agents, in the order they will be worked. */
  taskQueue?: FakeQueuedTask[];
  /** The open issues each workspace's repository offers, per workspace. */
  issues?: Record<string, FakeOfferedIssue[]>;
};

export type FakeQueuedTask = {
  id: string;
  owner: string;
  repository: string;
  number: number;
  title: string;
  body: string;
  url: string;
  queuedAt: string;
  targetWorkspaceId: string | null;
  heldByWorkspaceId: string | null;
};

export type FakeOfferedIssue = {
  id: string;
  number: number;
  title: string;
  body: string;
  createdAt: string;
  updatedAt: string;
  url: string;
};

export function cloneDefinition(definition: FakeServerDefinition): FakeServerDefinition {
  return structuredClone(definition);
}
