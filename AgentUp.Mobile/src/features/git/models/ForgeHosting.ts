export type ForgeAvailability = 'none' | 'needsCredential' | 'unreachable' | 'noProposal' | 'ready';

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

export type ForgeHosting = {
  availability: ForgeAvailability;
  moduleId: string | null;
  proposal: ForgeProposal | null;
  checks: ForgeCheck[];
  threads: ForgeReviewThread[];
  mergeability: ForgeMergeability | null;
  detail: string | null;
};
