/** What a product owner made of a decision, or that nobody has yet. */
export type DecisionVerdict = 'pending' | 'good' | 'bad';

/** Which part of the product a decision is about. */
export type DecisionCategoryId = 'product-vision' | 'gui-decisions' | 'code-architecture';

/** A picture behind a decision, kept by the backlog itself rather than by the run that took it. */
export type DecisionEvidence = {
  evidenceId: string;
  sourceArtifactId: string;
  mimeType: string;
};

/** One decision an agent took, and whatever the product owner has made of it. */
export type ProductDecision = {
  id: string;
  owner: string;
  repository: string;
  workspaceId: string;
  agent: string;
  category: DecisionCategoryId;
  headline: string;
  detail: string;
  evidence: string;
  evidenceImages: DecisionEvidence[];
  recordedAt: string;
  verdict: DecisionVerdict;
  note: string | null;
  reviewedAt: string | null;
};

/** How much of one category is waiting in one repository. */
export type DecisionCategoryCount = {
  category: DecisionCategoryId;
  label: string;
  pending: number;
  reviewed: number;
};

/** One repository's tile on the Train tab. */
export type DecisionRepository = {
  owner: string;
  repository: string;
  pending: number;
  reviewed: number;
  categories: DecisionCategoryCount[];
};

/** The three categories in the order the tab shows them. */
export const decisionCategories: { id: DecisionCategoryId; label: string }[] = [
  { id: 'product-vision', label: 'Product vision' },
  { id: 'gui-decisions', label: 'GUI decisions' },
  { id: 'code-architecture', label: 'Code architecture' },
];

/** A repository as one string, which is how a tile is identified. */
export function repositoryKey(repository: Pick<DecisionRepository, 'owner' | 'repository'>): string {
  return `${repository.owner}/${repository.repository}`;
}

/**
 * Where a swipe lands.
 *
 * A deck is walked one card at a time and does not wrap: running off either end means there is
 * nothing more that way, which is information a reader wants rather than a loop back to the start.
 */
export function nextCard(index: number, direction: 1 | -1, count: number): number {
  if (count <= 0) return 0;
  return Math.min(Math.max(index + direction, 0), count - 1);
}

/**
 * Whether a swipe of this length counts as a swipe at all.
 *
 * A reader scrolling the card's own text drags it a little; moving to the next decision is a
 * deliberate throw. The threshold is what separates the two, so reading never advances the deck.
 */
export function swipeTaken(dy: number, threshold: number): 1 | -1 | 0 {
  if (dy <= -threshold) return 1;
  if (dy >= threshold) return -1;
  return 0;
}

/** The decisions of one category, in the order a reviewer meets them. */
export function inCategory(decisions: ProductDecision[], category: DecisionCategoryId): ProductDecision[] {
  return decisions.filter(decision => decision.category === category);
}

/** How many of these nobody has judged yet, which is what the tab counts. */
export function pendingCount(decisions: ProductDecision[]): number {
  return decisions.filter(decision => decision.verdict === 'pending').length;
}

/**
 * The card to land on when a deck is opened: the first one nobody has judged, because that is
 * what the reader came for. A deck with nothing waiting opens at the start.
 */
export function firstUnjudged(decisions: ProductDecision[]): number {
  const index = decisions.findIndex(decision => decision.verdict === 'pending');
  return index < 0 ? 0 : index;
}

/** How a decision's own words read under its headline. */
export function recordedBy(decision: ProductDecision): string {
  return `${decision.agent} · ${decisionCategories.find(category => category.id === decision.category)?.label ?? decision.category}`;
}
