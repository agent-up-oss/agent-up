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
  /** The product decisions the agents have recorded, for the Train tab to judge. */
  productDecisions?: FakeProductDecision[];
  /** The pictures behind those decisions, by evidence id. */
  decisionEvidence?: Record<string, FakeDecisionEvidence>;
};

/** One recorded product decision, and whatever the product owner made of it. */
export type FakeProductDecision = {
  id: string;
  owner: string;
  repository: string;
  workspaceId: string;
  agent: string;
  category: string;
  headline: string;
  detail: string;
  evidence: string;
  evidenceImages: { evidenceId: string; sourceArtifactId: string; mimeType: string }[];
  recordedAt: string;
  verdict: string;
  note: string | null;
  reviewedAt: string | null;
};

/** A picture behind a decision, carried in the Demo as bytes rather than as a file. */
export type FakeDecisionEvidence = {
  mimeType: string;
  base64: string;
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
