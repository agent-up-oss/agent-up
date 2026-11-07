export type ForgeAvailability =
  | 'none'
  | 'notInstalled'
  | 'needsCredential'
  | 'noRemote'
  | 'unreachable'
  | 'noProposal'
  | 'ready';

export type ForgeProposal = {
  id: string;
  number: string;
  title: string;
  url: string;
  headRef: string;
  baseRef: string;
  state: string;
};

export type ForgeCheck = {
  name: string;
  status: string;
  conclusion: string;
  url: string;
};

export type ForgeReviewThread = {
  id: string;
  url: string;
  summary: string;
  isResolved: boolean;
};

export type ForgeMergeability = {
  mergeable: boolean;
  state: string;
};

/**
 * The account this workspace acts as on its host. It belongs to the workspace rather than to the
 * person operating it, so everything the agent pushes, comments, or reviews is attributable to it.
 */
export type ForgeAccount = {
  login: string;
  displayName: string;
  isBot: boolean;
  url: string;
};

export type ForgeHosting = {
  availability: ForgeAvailability;
  moduleId: string | null;
  proposal: ForgeProposal | null;
  checks: ForgeCheck[];
  threads: ForgeReviewThread[];
  mergeability: ForgeMergeability | null;
  detail: string | null;
  remedy: string | null;
  account?: ForgeAccount | null;
  reviews?: ForgeReview[] | null;
};

/** A verdict left on the proposal; the state is the host's own word for it. */
export type ForgeReview = {
  login: string;
  state: string;
};

export type ForgeLoginStart = {
  authorizationUrl: string;
};
