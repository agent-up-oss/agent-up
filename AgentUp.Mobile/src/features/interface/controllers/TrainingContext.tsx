import { useCallback, useEffect, useMemo, useState } from 'react';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type {
  DecisionCategoryId,
  DecisionRepository,
  DecisionVerdict,
  ProductDecision,
} from '../models/ProductDecision';
import { readDecisions, readRepositories, reviewDecision } from '../providers/TrainingProvider';

export type TrainingState = {
  /** Every repository an agent has decided something in. */
  repositories: DecisionRepository[];
  /** The decisions of the repository that is open, across all three categories. */
  decisions: ProductDecision[];
  loading: boolean;
  read: boolean;
  error: string | null;
  refresh: () => void;
  /** Opens one repository's backlog, or closes it when given null. */
  open: (repository: DecisionRepository | null) => void;
  /** The repository whose backlog is open. */
  opened: DecisionRepository | null;
  /** Sends a verdict, and holds it on screen while the Server catches up. */
  judge: (decision: ProductDecision, verdict: DecisionVerdict, note: string | null) => Promise<void>;
};

/**
 * The backlog of decisions the agents have taken, and the verdicts a product owner gives them.
 *
 * A verdict is applied here as soon as it is sent rather than waited for: the reader has already
 * moved to the next card, and a card that snapped back to unjudged while they read the next one
 * would be read as the Server refusing them.
 */
export function useTraining(): TrainingState {
  const { server } = useWorkspaces();
  const [repositories, setRepositories] = useState<DecisionRepository[]>([]);
  const [decisions, setDecisions] = useState<ProductDecision[]>([]);
  const [opened, setOpened] = useState<DecisionRepository | null>(null);
  const [loading, setLoading] = useState(false);
  const [read, setRead] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);

  const refresh = useCallback(() => setNonce(current => current + 1), []);

  useEffect(() => {
    if (!server) return;
    let current = true;
    setLoading(true);
    readRepositories(server)
      .then(found => {
        if (!current) return;
        setRepositories(found);
        setError(null);
        setRead(true);
      })
      .catch(cause => { if (current) setError(message(cause)); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [server, nonce]);

  useEffect(() => {
    if (!server || !opened) {
      setDecisions([]);
      return;
    }

    let current = true;
    readDecisions(server, opened.owner, opened.repository, null)
      .then(found => { if (current) setDecisions(found); })
      .catch(cause => { if (current) setError(message(cause)); });
    return () => { current = false; };
  }, [server, opened, nonce]);

  const judge = useCallback(async (
    decision: ProductDecision,
    verdict: DecisionVerdict,
    note: string | null,
  ) => {
    setDecisions(current => current.map(item =>
      item.id === decision.id
        ? { ...item, verdict, note, reviewedAt: new Date().toISOString() }
        : item));

    if (!server) return;
    try {
      await reviewDecision(server, decision.id, verdict, note);
      setError(null);
    } catch (cause) {
      // The verdict did not land, so the card goes back to waiting rather than lying about it.
      setDecisions(current => current.map(item =>
        item.id === decision.id ? { ...decision } : item));
      setError(message(cause));
    }
  }, [server]);

  const open = useCallback((repository: DecisionRepository | null) => setOpened(repository), []);

  return useMemo(() => ({
    repositories,
    decisions,
    loading,
    read,
    error,
    refresh,
    open,
    opened,
    judge,
  }), [repositories, decisions, loading, read, error, refresh, open, opened, judge]);
}

function message(cause: unknown): string {
  return cause instanceof Error ? cause.message : 'The Server could not be reached.';
}

/** The categories of the repository that is open, for the row of three under its name. */
export function categoryCounts(
  repository: DecisionRepository | null,
  category: DecisionCategoryId,
): { pending: number; reviewed: number } {
  const found = repository?.categories.find(item => item.category === category);
  return { pending: found?.pending ?? 0, reviewed: found?.reviewed ?? 0 };
}
