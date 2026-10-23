import { useCallback, useEffect, useMemo, useState } from 'react';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { TeamMember, TeamStanding } from '../models/TeamStanding';
import { readStandings } from '../providers/TeamStandingProvider';
import { triage, type TeamTriage } from '../services/TeamStandingService';

/**
 * Triage for the whole team, read from the two routes a Server answers per workspace. It re-reads
 * when the workspace list changes and on a pull, and it reports `read` so an empty bridge can tell
 * apart "nothing is waiting" from "not yet loaded" — an empty triage list is the good outcome.
 */
export function useTeamStanding(): TeamTriage & { loading: boolean; read: boolean; error: string | null; refresh: () => void } {
  const { server, workspaces, error: workspaceError } = useWorkspaces();
  const [standings, setStandings] = useState<TeamStanding[]>([]);
  const [loading, setLoading] = useState(false);
  const [read, setRead] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const members = useMemo<TeamMember[]>(
    () => workspaces.map(workspace => ({
      workspaceId: workspace.id,
      displayName: workspace.displayName,
      branch: workspace.branch,
      state: workspace.state,
    })),
    [workspaces],
  );
  const signature = useMemo(() => JSON.stringify(members), [members]);

  const load = useCallback(async () => {
    if (!server) return;
    setLoading(true);
    try {
      setStandings(await readStandings(server, JSON.parse(signature) as TeamMember[]));
      setError(null);
      setRead(true);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Could not read the team.');
    } finally {
      setLoading(false);
    }
  }, [server, signature]);

  useEffect(() => { void load(); }, [load]);

  return {
    ...triage(standings),
    loading,
    read,
    error: error ?? workspaceError,
    refresh: () => { void load(); },
  };
}
