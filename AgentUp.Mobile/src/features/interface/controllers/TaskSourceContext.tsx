import { useCallback, useEffect, useMemo, useState } from 'react';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { TaskSource, TaskSourceRepository } from '../models/TaskSource';
import { forgetTaskSource, readRepositories, readTaskSource, saveTaskSourceToken } from '../providers/TaskSourceProvider';

export type TaskSourceState = {
  source: TaskSource | null;
  repositories: TaskSourceRepository[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
  save: (moduleId: string, token: string) => Promise<void>;
  forget: (moduleId: string) => Promise<void>;
};

/**
 * The GitHub and Forgejo connections. A personal access token is sent to the Server and is not
 * kept here: the Server checks that it can list repositories and read pull requests.
 */
export function useTaskSource(): TaskSourceState {
  const { server } = useWorkspaces();
  const [source, setSource] = useState<TaskSource | null>(null);
  const [repositories, setRepositories] = useState<TaskSourceRepository[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);

  const refresh = useCallback(() => setNonce(current => current + 1), []);

  useEffect(() => {
    if (!server) return;
    let current = true;
    setLoading(true);
    Promise.all([readTaskSource(server), readRepositories(server)])
      .then(([found, listed]) => {
        if (!current) return;
        setSource(found);
        setRepositories(listed);
        setError(null);
      })
      .catch(cause => { if (current) setError(message(cause)); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [server, nonce]);

  const save = useCallback(async (moduleId: string, token: string) => {
    if (!server) return;
    try {
      setSource(await saveTaskSourceToken(server, moduleId, token));
      setRepositories(await readRepositories(server));
      setError(null);
    } catch (cause) {
      setError(message(cause));
    }
  }, [server]);

  const forget = useCallback(async (moduleId: string) => {
    if (!server) return;
    try {
      setSource(await forgetTaskSource(server, moduleId));
      setRepositories(await readRepositories(server));
      setError(null);
    } catch (cause) {
      setError(message(cause));
    }
  }, [server]);

  return useMemo(
    () => ({ source, repositories, loading, error, refresh, save, forget }),
    [source, repositories, loading, error, refresh, save, forget],
  );
}

function message(cause: unknown): string {
  return cause instanceof Error ? cause.message : 'The Server could not be reached.';
}
