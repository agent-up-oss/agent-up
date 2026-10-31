import { useCallback, useEffect, useMemo, useState } from 'react';
import { Linking } from 'react-native';
import { useWorkspaces } from '@/features/workspaces/controllers/WorkspacesContext';
import type { TaskSource } from '../models/TaskSource';
import {
  clearTaskSource,
  readTaskSource,
  setTaskSource,
  startTaskSourceLogin,
} from '../providers/TaskSourceProvider';

export type TaskSourceState = {
  source: TaskSource | null;
  loading: boolean;
  error: string | null;
  refresh: () => void;
  save: (moduleId: string, owner: string, repository: string) => Promise<void>;
  forget: () => Promise<void>;
  /** Opens the host's authorize page. Signing in finishes there, not here. */
  connect: () => Promise<void>;
};

/**
 * Where this Server's agents take their work from.
 *
 * The sign-in leaves the app: the reader authorizes at the repository host and the host calls the
 * Server back. Nothing here waits for that, so the source is re-read when the reader returns
 * rather than guessed at from the fact that a browser was opened.
 */
export function useTaskSource(): TaskSourceState {
  const { server } = useWorkspaces();
  const [source, setSource] = useState<TaskSource | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [nonce, setNonce] = useState(0);

  const refresh = useCallback(() => setNonce(current => current + 1), []);

  useEffect(() => {
    if (!server) return;
    let current = true;
    setLoading(true);
    readTaskSource(server)
      .then(found => { if (current) { setSource(found); setError(null); } })
      .catch(cause => { if (current) setError(message(cause)); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [server, nonce]);

  const save = useCallback(async (moduleId: string, owner: string, repository: string) => {
    if (!server) return;
    try {
      setSource(await setTaskSource(server, moduleId, owner, repository));
      setError(null);
    } catch (cause) {
      setError(message(cause));
    }
  }, [server]);

  const forget = useCallback(async () => {
    if (!server) return;
    try {
      setSource(await clearTaskSource(server));
      setError(null);
    } catch (cause) {
      setError(message(cause));
    }
  }, [server]);

  const connect = useCallback(async () => {
    if (!server) return;
    try {
      const url = await startTaskSourceLogin(server);
      if (!url) {
        setError('This Server could not start a sign-in for that host.');
        return;
      }
      setError(null);
      await Linking.openURL(url);
    } catch (cause) {
      setError(message(cause));
    }
  }, [server]);

  return useMemo(
    () => ({ source, loading, error, refresh, save, forget, connect }),
    [source, loading, error, refresh, save, forget, connect],
  );
}

function message(cause: unknown): string {
  return cause instanceof Error ? cause.message : 'The Server could not be reached.';
}
