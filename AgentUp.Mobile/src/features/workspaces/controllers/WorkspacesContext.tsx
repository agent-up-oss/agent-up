import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type PropsWithChildren } from 'react';
import { useServers } from '@/features/servers/controllers/ServersContext';
import type { CloneSourceRequest, Workspace } from '../models/Workspace';
import { cloneSourceRepository, listWorkspaces, startWorkspace, stopWorkspace } from '../providers/WorkspacesApiProvider';
import { createWorkspaceActions, type WorkspaceActions } from '../providers/WorkspaceActionsProvider';
import { createWorkspaceRefresh, type WorkspaceRefresher } from '../providers/WorkspaceRefreshProvider';

type WorkspacesController = {
  serverUrl: string | null;
  workspaces: Workspace[];
  selectedWorkspace: Workspace | null;
  loading: boolean;
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
  const serverUrl = activeServer?.url ?? null;
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // useState setters are stable, so these are created once and keep their own generation and
  // active-server state across renders. All request sequencing lives in the providers.
  const refresherRef = useRef<WorkspaceRefresher | null>(null);
  refresherRef.current ??= createWorkspaceRefresh({
    onLoading: setLoading,
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

  const refresh = useCallback(() => refresherRef.current!.refresh(serverUrl), [serverUrl]);

  useEffect(() => { void refresh(); }, [refresh]);

  const controller = useMemo<WorkspacesController>(() => ({
    serverUrl,
    workspaces,
    selectedWorkspace: workspaces.find(workspace => workspace.id === selectedId) ?? null,
    loading,
    error,
    selectWorkspace: id => setSelectedId(id),
    refresh,
    clone: request => actionsRef.current!.clone(serverUrl, request),
    start: id => actionsRef.current!.start(serverUrl, id),
    stop: id => actionsRef.current!.stop(serverUrl, id),
  }), [serverUrl, workspaces, selectedId, loading, error, refresh]);

  return <Context.Provider value={controller}>{children}</Context.Provider>;
}

export function useWorkspaces(): WorkspacesController {
  const value = useContext(Context);
  if (!value) throw new Error('useWorkspaces must be used inside WorkspacesProvider.');
  return value;
}
