import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react';
import { useServers } from '@/features/servers/controllers/ServersContext';
import type { ServerSession } from '@/features/servers/providers/ServerRequestProvider';
import type { CloneSourceRequest, Workspace } from '../models/Workspace';
import { cloneSourceRepository, listWorkspaces, startWorkspace, stopWorkspace } from '../providers/WorkspacesApiProvider';
import { createWorkspaceActions, type WorkspaceActions } from '../providers/WorkspaceActionsProvider';
import { createWorkspaceRefresh, type WorkspaceRefresher } from '../providers/WorkspaceRefreshProvider';

type WorkspacesController = {
  server: ServerSession | null;
  workspaces: Workspace[];
  selectedWorkspace: Workspace | null;
  loading: boolean;
  // False until the first refresh for the current server has settled. Routes deep-linked to a
  // workspace must wait for this: the list starts empty with loading false, so redirecting on a
  // missing workspace before the first load would discard a valid link.
  ready: boolean;
  error: string | null;
  selectWorkspace(id: string): void;
  refresh(): Promise<void>;
  clone(request: CloneSourceRequest): Promise<Workspace>;
  start(id: string): Promise<void>;
  stop(id: string): Promise<void>;
};

const Context = createContext<WorkspacesController | null>(null);

export function WorkspacesProvider({ children }: PropsWithChildren) {
  const { activeServer } = useServers();
  // The URL and the token both matter to a request, so the effect below re-runs when either
  // changes: signing in must reload the workspaces that were refused while unauthenticated.
  const serverUrl = activeServer?.url ?? null;
  const accessToken = activeServer?.accessToken;
  const server = useMemo<ServerSession | null>(
    () => (serverUrl ? { url: serverUrl, accessToken } : null),
    [serverUrl, accessToken],
  );
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // useState setters are stable, so these are created once and keep their own generation and
  // active-server state across renders. All request sequencing lives in the providers.
  const refresherRef = useRef<WorkspaceRefresher | null>(null);
  refresherRef.current ??= createWorkspaceRefresh({
    onLoading: loadingNow => {
      setLoading(loadingNow);
      // A refresh that has stopped loading has settled, whether it loaded, failed, or found no
      // server, so the routes can stop waiting and decide.
      if (!loadingNow) setReady(true);
    },
    onWorkspaces: loaded => {
      setWorkspaces(loaded);
      setError(null);
      setSelectedId(current => (current && loaded.some(w => w.id === current) ? current : loaded[0]?.id ?? null));
    },
    onError: message => {
      setWorkspaces([]);
      setError(message);
    },
    onDisconnected: () => {
      setWorkspaces([]);
      setSelectedId(null);
      setError(null);
    },
  }, listWorkspaces);

  const actionsRef = useRef<WorkspaceActions | null>(null);
  actionsRef.current ??= createWorkspaceActions(
    refresherRef.current,
    { clone: cloneSourceRepository, start: startWorkspace, stop: stopWorkspace },
    { onSelect: setSelectedId },
  );

  const refresh = useCallback(() => refresherRef.current!.refresh(server), [server]);

  useEffect(() => { void refresh(); }, [refresh]);

  const controller = useMemo<WorkspacesController>(() => ({
    server,
    workspaces,
    selectedWorkspace: workspaces.find(workspace => workspace.id === selectedId) ?? null,
    loading,
    ready,
    error,
    selectWorkspace: id => setSelectedId(id),
    refresh,
    clone: request => actionsRef.current!.clone(server, request),
    start: id => actionsRef.current!.start(server, id),
    stop: id => actionsRef.current!.stop(server, id),
  }), [server, workspaces, selectedId, loading, ready, error, refresh]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useWorkspaces(): WorkspacesController {
  const value = useContext(Context);
  if (!value) throw new Error('useWorkspaces must be used inside WorkspacesProvider.');
  return value;
}
